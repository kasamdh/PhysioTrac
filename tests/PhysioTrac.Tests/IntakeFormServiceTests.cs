using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Intake;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Template versioning/resolution (structurally identical to
/// ClinicalTemplateServiceTests/ConsentTemplateServiceTests) plus the
/// patient-submission half unique to intake forms.</summary>
public class IntakeFormServiceTests
{
    private const string SampleSchema = """{"sections":[{"key":"contact","fields":[{"key":"phone","type":"text"}]}]}""";

    private static (PhysioTracDbContext Db, IntakeFormService Service, Organization Org, Patient Patient)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient
        {
            OrganizationId = org.Id,
            FirstName = "Pat",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PortalUserId = Guid.NewGuid(),
        };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new IntakeFormService(db, tenantAccess, audit);
        return (db, service, org, patient);
    }

    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };
    private static TestCurrentUser SuperAdmin() => new() { UserId = Guid.NewGuid(), OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true };
    private static TestCurrentUser PortalPatient(Patient patient) => new() { UserId = patient.PortalUserId!.Value, OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

    [Fact]
    public async Task CreateTemplate_SecondVersionForTheSameKey_DeactivatesThePrevious()
    {
        var (db, service, org, _) = NewService();
        var admin = Admin(org.Id);
        var v1 = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake v1", SampleSchema), admin);
        var v2 = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake v2", SampleSchema), admin);

        Assert.Equal(2, v2.Version);
        var reloadedV1 = await db.IntakeFormTemplates.FindAsync(v1.Id);
        Assert.False(reloadedV1!.IsActive);
    }

    [Fact]
    public async Task CreateTemplate_KeyIsNormalizedToLowercase()
    {
        var (_, service, org, _) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("New-Patient-INTAKE", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));

        Assert.Equal("new-patient-intake", template.Key);
    }

    [Fact]
    public async Task CreateTemplate_InvalidJson_Throws()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("bad", TemplateScope.Organization, null, null, "Bad", "{not json"), Admin(org.Id)));
    }

    [Fact]
    public async Task CreateTemplate_PlatformScope_RequiresPlatformSuperAdmin()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("global-intake", TemplateScope.Platform, null, null, "Global", SampleSchema), Admin(org.Id)));
    }

    [Fact]
    public async Task ListResolvedForOrganization_ReturnsOneRowPerDistinctKey_ResolvedToItsBestMatch()
    {
        var (db, service, org, _) = NewService();
        var admin = Admin(org.Id);

        await service.CreateTemplateAsync(new CreateIntakeFormTemplateRequest("global-form", TemplateScope.Platform, null, null, "Global", SampleSchema), SuperAdmin());
        await service.CreateTemplateAsync(new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Our Intake", SampleSchema), admin);

        var resolved = await service.ListResolvedForOrganizationAsync(admin, null, null);

        Assert.Equal(2, resolved.Count);
        Assert.Contains(resolved, t => t.Key == "global-form");
        Assert.Contains(resolved, t => t.Key == "new-patient-intake" && t.Name == "Our Intake");
    }

    [Fact]
    public async Task Submit_ValidRequest_SnapshotsTemplateVersion()
    {
        var (db, service, org, patient) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));

        var submission = await service.SubmitAsync(
            PortalPatient(patient), new SubmitIntakeFormRequest(template.Id, """{"phone":"555-0100"}"""));

        Assert.Equal(patient.Id, submission.PatientId);
        Assert.Equal(1, submission.TemplateVersion);
        Assert.Equal(IntakeFormSubmissionStatus.Submitted, submission.Status);
    }

    [Fact]
    public async Task Submit_InvalidResponseJson_Throws()
    {
        var (_, service, org, patient) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitAsync(PortalPatient(patient), new SubmitIntakeFormRequest(template.Id, "{not json")));
    }

    [Fact]
    public async Task Submit_ByStaffRole_ThrowsForbidden()
    {
        var (_, service, org, patient) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SubmitAsync(Admin(org.Id), new SubmitIntakeFormRequest(template.Id, "{}")));
    }

    [Fact]
    public async Task Submit_AgainstAnotherOrganizationsTemplate_ThrowsNotFound()
    {
        var (db, service, _, patient) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b" };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgTemplate = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Their intake", SampleSchema), Admin(otherOrg.Id));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SubmitAsync(PortalPatient(patient), new SubmitIntakeFormRequest(otherOrgTemplate.Id, "{}")));
    }

    [Fact]
    public async Task Submit_AgainstASupersededTemplateVersion_ThrowsNotFound()
    {
        var (db, service, org, patient) = NewService();
        var admin = Admin(org.Id);
        var v1 = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "v1", SampleSchema), admin);
        await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "v2", SampleSchema), admin);

        // v1 is now inactive -- a stale client-cached template id must not
        // still be submittable.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SubmitAsync(PortalPatient(patient), new SubmitIntakeFormRequest(v1.Id, "{}")));
    }

    [Fact]
    public async Task ListSubmissionsForPatient_AnotherOrganizationsPatient_ThrowsForbidden()
    {
        var (db, service, org, patient) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));
        await service.SubmitAsync(PortalPatient(patient), new SubmitIntakeFormRequest(template.Id, "{}"));

        var otherOrg = new Organization { Name = "Client B", Slug = "client-b" };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgActor = Admin(otherOrg.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListSubmissionsForPatientAsync(patient.Id, otherOrgActor));
    }

    [Fact]
    public async Task Review_MarksSubmissionReviewed()
    {
        var (db, service, org, patient) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));
        var submission = await service.SubmitAsync(PortalPatient(patient), new SubmitIntakeFormRequest(template.Id, "{}"));

        var reviewed = await service.ReviewAsync(submission.Id, Admin(org.Id));

        Assert.Equal(IntakeFormSubmissionStatus.Reviewed, reviewed.Status);
        Assert.NotNull(reviewed.ReviewedAt);
    }

    [Fact]
    public async Task Review_ByPatientRole_ThrowsForbidden()
    {
        var (db, service, org, patient) = NewService();
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", SampleSchema), Admin(org.Id));
        var submission = await service.SubmitAsync(PortalPatient(patient), new SubmitIntakeFormRequest(template.Id, "{}"));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewAsync(submission.Id, PortalPatient(patient)));
    }
}

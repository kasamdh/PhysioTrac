using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Consents;
using PhysioTrac.Application.Documents;
using PhysioTrac.Application.Intake;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Phase 6's explicit isolation requirement: a patient portal
/// account must never reach another patient's records, even within the same
/// organization -- the harder case than cross-org isolation (already
/// covered elsewhere), since here OrganizationId alone can't distinguish
/// them; only TenantAccessService.PatientsFor's PortalUserId-first check
/// can. One test per patient-facing resource this phase touches or adds,
/// each hitting the real service directly (not just the shared
/// TenantAccessServiceTests-level guarantee) so a future resource that
/// forgets to route through RequirePatientAccessAsync/RequirePortalPatientAsync
/// fails here specifically.</summary>
public class PatientPortalIsolationTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private record TwoPatients(PhysioTracDbContext Db, Organization Org, Patient PatientA, Patient PatientB, TestCurrentUser PortalA, TestCurrentUser PortalB);

    /// <summary>Two patients, same organization, each with their own working
    /// portal login -- the exact scenario the phase spec calls out by name.</summary>
    private static TwoPatients TwoPatientsSameOrg()
    {
        var db = NewDb();
        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var portalAId = Guid.NewGuid();
        var portalBId = Guid.NewGuid();
        var patientA = new Patient { OrganizationId = org.Id, FirstName = "Alex", LastName = "A", DateOfBirth = new DateOnly(1990, 1, 1), PortalUserId = portalAId };
        var patientB = new Patient { OrganizationId = org.Id, FirstName = "Blair", LastName = "B", DateOfBirth = new DateOnly(1991, 2, 2), PortalUserId = portalBId };
        db.Organizations.Add(org);
        db.Patients.AddRange(patientA, patientB);
        db.SaveChanges();

        var portalA = new TestCurrentUser { UserId = portalAId, OrganizationId = org.Id, Role = UserRole.Patient };
        var portalB = new TestCurrentUser { UserId = portalBId, OrganizationId = org.Id, Role = UserRole.Patient };
        return new TwoPatients(db, org, patientA, patientB, portalA, portalB);
    }

    [Fact]
    public async Task HomeExercisePrograms_PatientCannotListAnotherPatientsPrograms_SameOrg()
    {
        var (db, org, patientA, patientB, portalA, portalB) = TwoPatientsSameOrg();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new HomeExerciseProgramService(db, tenantAccess, audit);
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        // Both patients need a caseload-visible therapist to create for them.
        patientA.AssignedTherapistId = therapist.UserId;
        patientB.AssignedTherapistId = therapist.UserId;
        await db.SaveChangesAsync();

        await service.CreateAsync(new CreateHomeExerciseProgramRequest(patientB.Id, "Blair's program", null, new List<CreateHomeExerciseItemRequest>()), therapist);

        // Patient A (portal) can see nothing for Patient B, even by guessing
        // Patient B's real id.
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListForPatientAsync(patientB.Id, portalA));

        // Sanity: Patient B's own portal login DOES see it.
        var ownList = await service.ListForPatientAsync(patientB.Id, portalB);
        Assert.Single(ownList);
    }

    [Fact]
    public async Task Documents_PatientCannotListOrDownloadAnotherPatientsDocuments_SameOrg()
    {
        var (db, org, patientA, patientB, portalA, portalB) = TwoPatientsSameOrg();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var storage = new InMemoryFileStorage();
        var options = Options.Create(new StorageOptions());
        var service = new DocumentService(db, tenantAccess, storage, audit, options);
        var frontDesk = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };

        var uploaded = await service.UploadAsync(
            new UploadDocumentRequest(patientB.Id, DocumentCategory.Other, "b-file.pdf", "application/pdf", 5, null, new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })),
            frontDesk);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListForPatientAsync(patientB.Id, portalA));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DownloadAsync(uploaded.Id, portalA));

        var (document, content) = await service.DownloadAsync(uploaded.Id, portalB);
        await content.DisposeAsync();
        Assert.Equal(uploaded.Id, document.Id);
    }

    [Fact]
    public async Task Consents_PatientCannotListAnotherPatientsConsents_SameOrg()
    {
        var (db, org, patientA, patientB, portalA, portalB) = TwoPatientsSameOrg();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var templates = new ConsentTemplateService(db, tenantAccess, audit);
        var service = new ConsentService(db, tenantAccess, audit, templates);

        await service.RecordOwnAsync(portalB, new RecordOwnConsentRequest(ConsentType.ConsentToTreat, "Blair B"), null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListForPatientAsync(patientB.Id, portalA));

        var ownList = await service.ListForPatientAsync(patientB.Id, portalB);
        Assert.Single(ownList);
    }

    [Fact]
    public async Task Consents_PatientSelfSigningAlwaysSignsTheirOwnChart_NeverAnotherPatients()
    {
        // RecordOwnConsentRequest structurally has no PatientId field at all
        // -- there's no parameter for a patient to even attempt to supply
        // another patient's id through this path. This test documents that
        // invariant: signing via Patient A's own portal session always lands
        // on Patient A's chart.
        var (db, _, patientA, patientB, portalA, _) = TwoPatientsSameOrg();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var templates = new ConsentTemplateService(db, tenantAccess, audit);
        var service = new ConsentService(db, tenantAccess, audit, templates);

        var consent = await service.RecordOwnAsync(portalA, new RecordOwnConsentRequest(ConsentType.ConsentToTreat, "Alex A"), null);

        Assert.Equal(patientA.Id, consent.PatientId);
        Assert.NotEqual(patientB.Id, consent.PatientId);
    }

    [Fact]
    public async Task IntakeFormSubmissions_PatientCannotListAnotherPatientsSubmissions_SameOrg()
    {
        var (db, org, patientA, patientB, portalA, portalB) = TwoPatientsSameOrg();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new IntakeFormService(db, tenantAccess, audit);
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", "{}"), admin);

        await service.SubmitAsync(portalB, new SubmitIntakeFormRequest(template.Id, "{}"));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListSubmissionsForPatientAsync(patientB.Id, portalA));

        var ownList = await service.ListSubmissionsForPatientAsync(patientB.Id, portalB);
        Assert.Single(ownList);
    }

    [Fact]
    public async Task IntakeFormSubmissions_PatientSubmissionAlwaysAttachesToTheirOwnChart()
    {
        var (db, org, patientA, patientB, portalA, _) = TwoPatientsSameOrg();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new IntakeFormService(db, tenantAccess, audit);
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var template = await service.CreateTemplateAsync(
            new CreateIntakeFormTemplateRequest("new-patient-intake", TemplateScope.Organization, null, null, "Intake", "{}"), admin);

        // SubmitIntakeFormRequest also carries no patient id -- the portal
        // session is the only source of "which patient."
        var submission = await service.SubmitAsync(portalA, new SubmitIntakeFormRequest(template.Id, "{}"));

        Assert.Equal(patientA.Id, submission.PatientId);
        Assert.NotEqual(patientB.Id, submission.PatientId);
    }
}

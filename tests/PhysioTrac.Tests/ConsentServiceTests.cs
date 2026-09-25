using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

public class ConsentServiceTests
{
    private static (PhysioTracDbContext Db, ConsentService Service, Organization Org, Patient Patient, TestCurrentUser FrontDesk)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var templates = new ConsentTemplateService(db, tenantAccess, audit);
        var frontDesk = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };

        return (db, new ConsentService(db, tenantAccess, audit, templates), org, patient, frontDesk);
    }

    [Fact]
    public async Task Record_ValidSignature_SnapshotsConsentText()
    {
        var (_, service, _, patient, actor) = NewService();

        var consent = await service.RecordAsync(
            new RecordConsentRequest(patient.Id, ConsentType.ConsentToTreat, "Pat Patient"), actor, "127.0.0.1");

        Assert.Equal(ConsentTypeText.For(ConsentType.ConsentToTreat), consent.ConsentText);
        Assert.Equal("Pat Patient", consent.SignedByName);
        Assert.True(consent.IsActive);
        Assert.Equal("127.0.0.1", consent.IpAddress);
    }

    [Fact]
    public async Task Record_BlankSignatureName_Throws()
    {
        var (_, service, _, patient, actor) = NewService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.HipaaAcknowledgment, "   "), actor, null));
    }

    [Fact]
    public async Task Record_SameTypeTwice_CreatesTwoSeparateRows()
    {
        var (_, service, _, patient, actor) = NewService();

        await service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.FinancialPolicy, "Pat Patient"), actor, null);
        await service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.FinancialPolicy, "Pat Patient"), actor, null);

        var all = await service.ListForPatientAsync(patient.Id, actor);
        Assert.Equal(2, all.Count(c => c.ConsentType == ConsentType.FinancialPolicy));
    }

    [Fact]
    public async Task Revoke_ActiveConsent_Succeeds()
    {
        var (_, service, _, patient, actor) = NewService();
        var consent = await service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.TelehealthConsent, "Pat Patient"), actor, null);

        var revoked = await service.RevokeAsync(consent.Id, new RevokeConsentRequest("Patient requested paper copy instead"), actor);

        Assert.False(revoked.IsActive);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task Revoke_AlreadyRevoked_Throws()
    {
        var (_, service, _, patient, actor) = NewService();
        var consent = await service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.DryNeedlingConsent, "Pat Patient"), actor, null);
        await service.RevokeAsync(consent.Id, new RevokeConsentRequest(null), actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeAsync(consent.Id, new RevokeConsentRequest(null), actor));
    }

    [Fact]
    public async Task Record_ForAnotherOrganizationsPatient_ThrowsForbidden()
    {
        var (db, service, org, _, actor) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        var patientInOtherOrg = new Patient { OrganizationId = otherOrg.Id, FirstName = "Beta", LastName = "Brown", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(otherOrg);
        db.Patients.Add(patientInOtherOrg);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RecordAsync(new RecordConsentRequest(patientInOtherOrg.Id, ConsentType.ConsentToTreat, "Beta Brown"), actor, null));
        Assert.Empty(await db.Consents.ToListAsync());
    }

    [Fact]
    public async Task Revoke_ForAnotherOrganizationsConsent_ThrowsNotFound()
    {
        var (db, service, org, patient, actor) = NewService();
        var consent = await service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.ConsentToTreat, "Pat Patient"), actor, null);
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgActor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = otherOrg.Id, Role = UserRole.Scheduler };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RevokeAsync(consent.Id, new RevokeConsentRequest(null), otherOrgActor));
    }

    [Fact]
    public async Task Record_PatientRoleCannotRecordOnOwnBehalf()
    {
        var (_, service, _, patient, _) = NewService();
        var patientActor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.ConsentToTreat, "Pat Patient"), patientActor, null));
    }

    [Fact]
    public async Task RecordOwn_ValidSignature_ResolvesOwnPatientFromPortalSession()
    {
        var (db, service, _, patient, _) = NewService();
        var portalUserId = Guid.NewGuid();
        patient.PortalUserId = portalUserId;
        await db.SaveChangesAsync();
        var patientActor = new TestCurrentUser { UserId = portalUserId, OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

        var consent = await service.RecordOwnAsync(patientActor, new RecordOwnConsentRequest(ConsentType.HipaaAcknowledgment, "Pat Patient"), "203.0.113.5");

        Assert.Equal(patient.Id, consent.PatientId);
        Assert.Equal(portalUserId, consent.RecordedById);
        Assert.Equal("203.0.113.5", consent.IpAddress);
        // No ConsentTemplate configured in this in-memory db -- falls back
        // to the static ConsentTypeText constant, with no template version.
        Assert.Equal(ConsentTypeText.For(ConsentType.HipaaAcknowledgment), consent.ConsentText);
        Assert.Null(consent.TemplateVersion);
    }

    [Fact]
    public async Task RecordOwn_WhenAConsentTemplateIsConfigured_SnapshotsItsTextAndVersion()
    {
        var (db, service, org, patient, actor) = NewService();
        var portalUserId = Guid.NewGuid();
        patient.PortalUserId = portalUserId;
        await db.SaveChangesAsync();
        var patientActor = new TestCurrentUser { UserId = portalUserId, OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var templates = new ConsentTemplateService(db, tenantAccess, audit);
        var orgAdmin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        await templates.CreateAsync(new CreateConsentTemplateRequest(
            ConsentType.FinancialPolicy, TemplateScope.Organization, null, null, "Our custom financial policy language."), orgAdmin);

        var consent = await service.RecordOwnAsync(patientActor, new RecordOwnConsentRequest(ConsentType.FinancialPolicy, "Pat Patient"), null);

        Assert.Equal("Our custom financial policy language.", consent.ConsentText);
        Assert.Equal(1, consent.TemplateVersion);
    }

    [Fact]
    public async Task RecordOwn_ByStaffRole_ThrowsForbidden()
    {
        var (_, service, _, _, actor) = NewService();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RecordOwnAsync(actor, new RecordOwnConsentRequest(ConsentType.ConsentToTreat, "Pat Patient"), null));
    }
}

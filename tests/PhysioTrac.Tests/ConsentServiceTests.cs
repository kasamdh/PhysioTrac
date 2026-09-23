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
        var frontDesk = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };

        return (db, new ConsentService(db, tenantAccess, audit), org, patient, frontDesk);
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
    public async Task Record_PatientRoleCannotRecordOnOwnBehalf()
    {
        var (_, service, _, patient, _) = NewService();
        var patientActor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RecordAsync(new RecordConsentRequest(patient.Id, ConsentType.ConsentToTreat, "Pat Patient"), patientActor, null));
    }
}

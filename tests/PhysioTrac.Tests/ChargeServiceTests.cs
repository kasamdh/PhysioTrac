using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Billing;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors `Charge.clean()`'s capture rules from `care/models.py`:
/// CPT/modifier format, the units-override-reason requirement once a
/// recommendation exists, and cross-org/cross-patient FK checks.</summary>
public class ChargeServiceTests
{
    private static (PhysioTracDbContext Db, ChargeService Service, Organization Org, Patient Patient, TestCurrentUser Biller)
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
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        return (db, new ChargeService(db, tenantAccess), org, patient, biller);
    }

    private static CreateChargeRequest ValidRequest(Guid patientId, int? minutes = null, int units = 1, string? overrideReason = null) => new(
        patientId, null, Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), "97110",
        new List<string> { "GP" }, units, minutes, overrideReason, 75.00m, null);

    [Fact]
    public async Task Create_ComputesRecommendedUnitsFromMinutes_Via8MinuteRule()
    {
        var (_, service, _, patient, biller) = NewService();

        // 23 minutes -> 2 units per the 8-minute rule; matching units, no override needed.
        var charge = await service.CreateAsync(ValidRequest(patient.Id, minutes: 23, units: 2), biller);

        Assert.Equal(2, charge.RecommendedUnits);
        Assert.Equal(2, charge.Units);
    }

    [Fact]
    public async Task Create_UnitsDivergeFromRecommendation_WithoutReason_Throws()
    {
        var (_, service, _, patient, biller) = NewService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ValidRequest(patient.Id, minutes: 23, units: 3), biller));
    }

    [Fact]
    public async Task Create_UnitsDivergeFromRecommendation_WithReason_Succeeds()
    {
        var (_, service, _, patient, biller) = NewService();

        var charge = await service.CreateAsync(ValidRequest(patient.Id, minutes: 23, units: 3, overrideReason: "Payer requires flat 3 units"), biller);

        Assert.Equal(2, charge.RecommendedUnits);
        Assert.Equal(3, charge.Units);
        Assert.Equal(1, charge.UnitsDifference);
    }

    [Fact]
    public async Task Create_InvalidCptCodeFormat_Throws()
    {
        var (_, service, _, patient, biller) = NewService();
        var request = ValidRequest(patient.Id) with { CptCode = "abc" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, biller));
    }

    [Fact]
    public async Task Create_TooManyModifiers_Throws()
    {
        var (_, service, _, patient, biller) = NewService();
        var request = ValidRequest(patient.Id) with { Modifiers = new List<string> { "GP", "59", "KX", "GN", "GO" } };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, biller));
    }

    [Fact]
    public async Task Create_NoteFromAnotherPatient_Throws()
    {
        var (db, service, org, patient, biller) = NewService();
        var otherPatient = new Patient { OrganizationId = org.Id, FirstName = "Other", LastName = "Person", DateOfBirth = new DateOnly(1985, 1, 1) };
        db.Patients.Add(otherPatient);
        var note = new ClinicalNote { PatientId = otherPatient.Id, TherapistId = Guid.NewGuid(), ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.ClinicalNotes.Add(note);
        await db.SaveChangesAsync();

        var request = ValidRequest(patient.Id) with { ClinicalNoteId = note.Id };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, biller));
    }

    [Fact]
    public async Task Create_CrossOrganizationPatient_IsRejected()
    {
        var (_, service, _, _, biller) = NewService();
        var otherPatientId = Guid.NewGuid(); // never persisted, definitely not in this org

        await Assert.ThrowsAsync<PhysioTrac.Application.Common.NotFoundException>(() => service.CreateAsync(ValidRequest(otherPatientId), biller));
    }
}

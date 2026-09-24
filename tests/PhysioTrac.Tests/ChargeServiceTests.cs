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

    private static async Task<(ClinicalNote Note, Provider Provider)> SeedSignedNoteAsync(
        PhysioTracDbContext db, Organization org, Patient patient, params (InterventionCategory? Category, int Minutes, bool IsTimed)[] items)
    {
        var therapistUserId = Guid.NewGuid();
        var provider = new Provider { OrganizationId = org.Id, UserId = therapistUserId, FirstName = "Jamie", LastName = "Chen" };
        db.Providers.Add(provider);

        var note = new ClinicalNote
        {
            PatientId = patient.Id,
            TherapistId = therapistUserId,
            Status = NoteStatus.Signed,
            ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        var order = 0;
        foreach (var (category, minutes, isTimed) in items)
        {
            note.InterventionItems.Add(new NoteIntervention
            {
                Description = "Intervention", Category = category, Minutes = minutes, IsTimed = isTimed, Order = order++,
            });
        }
        db.ClinicalNotes.Add(note);
        await db.SaveChangesAsync();
        return (note, provider);
    }

    [Fact]
    public async Task GenerateFromNote_MappedTimedIntervention_CreatesChargeWithComputedUnitsAndFeeSchedulePrice()
    {
        var (db, service, org, patient, biller) = NewService();
        var (note, provider) = await SeedSignedNoteAsync(db, org, patient, (InterventionCategory.TherapeuticExercise, 15, true), (InterventionCategory.TherapeuticExercise, 10, true));

        db.CptCodeMappings.Add(new CptCodeMapping { OrganizationId = org.Id, InterventionCategory = InterventionCategory.TherapeuticExercise, CptCode = "97110", CreatedById = biller.UserId });
        db.ServicePrices.Add(new ServicePrice { OrganizationId = org.Id, CptCode = "97110", Label = "Therapeutic Exercise", Price = 65m });
        await db.SaveChangesAsync();

        var charges = await service.GenerateFromNoteAsync(note.Id, biller);

        var charge = Assert.Single(charges);
        Assert.Equal("97110", charge.CptCode);
        Assert.Equal(25, charge.Minutes); // 15 + 10 summed before applying the 8-minute rule
        Assert.Equal(2, charge.Units); // 25 minutes -> 2 units under the Medicare table
        Assert.Equal(65m, charge.ChargeAmount);
        Assert.Equal(provider.Id, charge.ProviderId);
        Assert.Equal(note.Id, charge.ClinicalNoteId);
    }

    [Fact]
    public async Task GenerateFromNote_LocationSpecificPrice_PreferredOverOrganizationWideDefault()
    {
        var (db, service, org, patient, biller) = NewService();
        var location = new Location { OrganizationId = org.Id, Name = "Downtown" };
        db.Locations.Add(location);
        patient.PrimaryLocationId = location.Id;
        await db.SaveChangesAsync();
        var (note, _) = await SeedSignedNoteAsync(db, org, patient, (InterventionCategory.ManualTherapy, 20, true));

        db.CptCodeMappings.Add(new CptCodeMapping { OrganizationId = org.Id, InterventionCategory = InterventionCategory.ManualTherapy, CptCode = "97140", CreatedById = biller.UserId });
        db.ServicePrices.AddRange(
            new ServicePrice { OrganizationId = org.Id, CptCode = "97140", Label = "Manual Therapy", Price = 55m },
            new ServicePrice { OrganizationId = org.Id, LocationId = location.Id, CptCode = "97140", Label = "Manual Therapy (Downtown)", Price = 70m });
        await db.SaveChangesAsync();

        var charges = await service.GenerateFromNoteAsync(note.Id, biller);

        Assert.Equal(70m, Assert.Single(charges).ChargeAmount);
    }

    [Fact]
    public async Task GenerateFromNote_UnmappedCategory_Throws()
    {
        var (db, service, org, patient, biller) = NewService();
        var (note, _) = await SeedSignedNoteAsync(db, org, patient, (InterventionCategory.GaitTraining, 20, true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateFromNoteAsync(note.Id, biller));
    }

    [Fact]
    public async Task GenerateFromNote_DraftNote_Throws()
    {
        var (db, service, org, patient, biller) = NewService();
        var note = new ClinicalNote
        {
            PatientId = patient.Id, TherapistId = Guid.NewGuid(), Status = NoteStatus.Draft, ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.ClinicalNotes.Add(note);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateFromNoteAsync(note.Id, biller));
    }

    [Fact]
    public async Task GenerateFromNote_CalledTwice_ThrowsOnTheSecondCall()
    {
        var (db, service, org, patient, biller) = NewService();
        var (note, _) = await SeedSignedNoteAsync(db, org, patient, (InterventionCategory.TherapeuticExercise, 20, true));
        db.CptCodeMappings.Add(new CptCodeMapping { OrganizationId = org.Id, InterventionCategory = InterventionCategory.TherapeuticExercise, CptCode = "97110", CreatedById = biller.UserId });
        db.ServicePrices.Add(new ServicePrice { OrganizationId = org.Id, CptCode = "97110", Label = "Therapeutic Exercise", Price = 65m });
        await db.SaveChangesAsync();

        await service.GenerateFromNoteAsync(note.Id, biller);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateFromNoteAsync(note.Id, biller));
    }

    [Fact]
    public async Task GenerateFromNote_UntimedIntervention_BillsOneUnitPerLine_WithNoMinutesRecorded()
    {
        var (db, service, org, patient, biller) = NewService();
        var (note, _) = await SeedSignedNoteAsync(db, org, patient, (InterventionCategory.PatientEducation, 0, false), (InterventionCategory.PatientEducation, 0, false));
        db.CptCodeMappings.Add(new CptCodeMapping { OrganizationId = org.Id, InterventionCategory = InterventionCategory.PatientEducation, CptCode = "97535", CreatedById = biller.UserId });
        db.ServicePrices.Add(new ServicePrice { OrganizationId = org.Id, CptCode = "97535", Label = "Self-Care/Home Mgmt Training", Price = 50m });
        await db.SaveChangesAsync();

        var charges = await service.GenerateFromNoteAsync(note.Id, biller);

        var charge = Assert.Single(charges);
        Assert.Equal(2, charge.Units);
        Assert.Null(charge.Minutes);
    }

    [Fact]
    public async Task GenerateFromAppointment_CompletedWithDefaultCptCode_CreatesFlatCharge()
    {
        var (db, service, org, patient, biller) = NewService();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var appointmentType = new AppointmentType { OrganizationId = org.Id, Name = "Follow-up", DefaultCptCode = "97110", Price = 95m };
        db.Providers.Add(provider);
        db.AppointmentTypes.Add(appointmentType);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id, TherapistId = Guid.NewGuid(), ProviderId = provider.Id, AppointmentTypeId = appointmentType.Id,
            Status = AppointmentStatus.Completed, StartsAt = DateTimeOffset.UtcNow, EndsAt = DateTimeOffset.UtcNow.AddMinutes(30), CreatedById = biller.UserId,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var charge = await service.GenerateFromAppointmentAsync(appointment.Id, biller);

        Assert.Equal("97110", charge.CptCode);
        Assert.Equal(95m, charge.ChargeAmount);
        Assert.Equal(1, charge.Units);
        Assert.Equal(appointment.Id, charge.AppointmentId);
    }

    [Fact]
    public async Task GenerateFromAppointment_NoDefaultCptCodeConfigured_Throws()
    {
        var (db, service, org, patient, biller) = NewService();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var appointmentType = new AppointmentType { OrganizationId = org.Id, Name = "Follow-up" };
        db.Providers.Add(provider);
        db.AppointmentTypes.Add(appointmentType);
        await db.SaveChangesAsync();
        var appointment = new Appointment
        {
            PatientId = patient.Id, TherapistId = Guid.NewGuid(), ProviderId = provider.Id, AppointmentTypeId = appointmentType.Id,
            Status = AppointmentStatus.Completed, StartsAt = DateTimeOffset.UtcNow, EndsAt = DateTimeOffset.UtcNow.AddMinutes(30), CreatedById = biller.UserId,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateFromAppointmentAsync(appointment.Id, biller));
    }

    [Fact]
    public async Task GenerateFromAppointment_NotCompleted_Throws()
    {
        var (db, service, org, patient, biller) = NewService();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var appointmentType = new AppointmentType { OrganizationId = org.Id, Name = "Follow-up", DefaultCptCode = "97110", Price = 95m };
        db.Providers.Add(provider);
        db.AppointmentTypes.Add(appointmentType);
        await db.SaveChangesAsync();
        var appointment = new Appointment
        {
            PatientId = patient.Id, TherapistId = Guid.NewGuid(), ProviderId = provider.Id, AppointmentTypeId = appointmentType.Id,
            Status = AppointmentStatus.Scheduled, StartsAt = DateTimeOffset.UtcNow, EndsAt = DateTimeOffset.UtcNow.AddMinutes(30), CreatedById = biller.UserId,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateFromAppointmentAsync(appointment.Id, biller));
    }
}

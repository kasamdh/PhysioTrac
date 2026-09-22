using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Booking;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

public class PortalBookingServiceTests
{
    private static (PhysioTracDbContext Db, PortalBookingService Service, Organization Org, Location Location, Provider Provider, AppointmentType Type, Patient Patient, TestCurrentUser PatientUser)
        SeedScenario()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var location = new Location { OrganizationId = org.Id, Name = "Main Clinic", Timezone = "UTC", IsActive = true };
        var providerUserId = Guid.NewGuid();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Terry", LastName = "Therapist", UserId = providerUserId, IsActive = true, OnlineBookingEnabled = true };
        var appointmentType = new AppointmentType { OrganizationId = org.Id, Name = "Follow-up", DefaultDurationMinutes = 30, IsActive = true, OnlineBookingEnabled = true, RequiresNewPatient = false };
        var config = new BookingConfiguration
        {
            OrganizationId = org.Id, OnlineBookingEnabled = true, AllowReturningPatients = true,
            MinNoticeHours = 0, MaxAdvanceDays = 365, SlotIntervalMinutes = 30, PatientChangeCutoffHours = 24,
        };
        var portalUserId = Guid.NewGuid();
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1), PortalUserId = portalUserId };

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var availability = new ProviderAvailability
        {
            ProviderId = provider.Id, LocationId = location.Id, DayOfWeek = tomorrow.ToWeekday(),
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Active = true,
        };
        var link = new ProviderAppointmentType { ProviderId = provider.Id, AppointmentTypeId = appointmentType.Id, Active = true };

        db.Organizations.Add(org);
        db.Locations.Add(location);
        db.Providers.Add(provider);
        db.AppointmentTypes.Add(appointmentType);
        db.BookingConfigurations.Add(config);
        db.Patients.Add(patient);
        db.ProviderAvailabilities.Add(availability);
        db.ProviderAppointmentTypes.Add(link);
        db.SaveChanges();
        provider.Locations.Add(location);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var availabilityService = new AvailabilityService(db);
        var service = new PortalBookingService(db, tenantAccess, availabilityService, audit);
        var patientUser = new TestCurrentUser { UserId = portalUserId, OrganizationId = org.Id, Role = UserRole.Patient };
        return (db, service, org, location, provider, appointmentType, patient, patientUser);
    }

    [Fact]
    public async Task Create_ValidSlot_Succeeds()
    {
        var (_, service, _, location, provider, type, patient, patientUser) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

        var appointment = await service.CreateAsync(patientUser, new PortalBookingRequest(location.Id, type.Id, provider.Id, nineAm, "Follow-up"));

        Assert.Equal(patient.Id, appointment.PatientId);
        Assert.Equal(BookingSource.PatientPortal, appointment.BookingSource);
    }

    [Fact]
    public async Task Create_NewPatientOnlyType_IsRejected()
    {
        var (db, service, org, location, provider, _, _, patientUser) = SeedScenario();
        var newPatientType = new AppointmentType { OrganizationId = org.Id, Name = "Initial Eval", IsActive = true, OnlineBookingEnabled = true, RequiresNewPatient = true };
        db.AppointmentTypes.Add(newPatientType);
        await db.SaveChangesAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

        await Assert.ThrowsAsync<BookingValidationException>(() =>
            service.CreateAsync(patientUser, new PortalBookingRequest(location.Id, newPatientType.Id, provider.Id, nineAm, null)));
    }

    [Fact]
    public async Task Cancel_WithinChangeCutoffWindow_IsRejected()
    {
        var (db, service, _, location, provider, type, patient, patientUser) = SeedScenario();
        var soon = DateTimeOffset.UtcNow.AddHours(2); // less than the 24h PatientChangeCutoffHours
        var appointment = new Appointment
        {
            PatientId = patient.Id, TherapistId = provider.UserId!.Value, ProviderId = provider.Id, LocationDetailId = location.Id,
            AppointmentTypeId = type.Id, Status = AppointmentStatus.Scheduled, StartsAt = soon, EndsAt = soon.AddMinutes(30), CreatedById = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ChangeCutoffException>(() => service.CancelAsync(patientUser, appointment.Id));
    }

    [Fact]
    public async Task Cancel_OutsideChangeCutoffWindow_Succeeds()
    {
        var (db, service, _, location, provider, type, patient, patientUser) = SeedScenario();
        var later = DateTimeOffset.UtcNow.AddDays(3);
        var appointment = new Appointment
        {
            PatientId = patient.Id, TherapistId = provider.UserId!.Value, ProviderId = provider.Id, LocationDetailId = location.Id,
            AppointmentTypeId = type.Id, Status = AppointmentStatus.Scheduled, StartsAt = later, EndsAt = later.AddMinutes(30), CreatedById = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var cancelled = await service.CancelAsync(patientUser, appointment.Id);

        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Cancel_AnotherPatientsAppointment_IsNotFound()
    {
        var (db, service, org, location, provider, type, _, patientUser) = SeedScenario();
        var otherPatient = new Patient { OrganizationId = org.Id, FirstName = "Other", LastName = "Person", DateOfBirth = new DateOnly(1985, 1, 1) };
        db.Patients.Add(otherPatient);
        var later = DateTimeOffset.UtcNow.AddDays(3);
        var appointment = new Appointment
        {
            PatientId = otherPatient.Id, TherapistId = provider.UserId!.Value, ProviderId = provider.Id, LocationDetailId = location.Id,
            AppointmentTypeId = type.Id, Status = AppointmentStatus.Scheduled, StartsAt = later, EndsAt = later.AddMinutes(30), CreatedById = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BookingNotFoundException>(() => service.CancelAsync(patientUser, appointment.Id));
    }

    [Fact]
    public async Task JoinWaitlist_ThenLeave_UpdatesStatus()
    {
        var (_, service, _, location, _, type, _, patientUser) = SeedScenario();

        var entry = await service.JoinWaitlistAsync(patientUser, new JoinWaitlistRequest(
            location.Id, type.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, "Any morning works"));
        Assert.Equal(WaitlistStatus.Active, entry.Status);

        var left = await service.LeaveWaitlistAsync(patientUser, entry.Id);
        Assert.Equal(WaitlistStatus.Cancelled, left.Status);
    }

    [Fact]
    public async Task JoinWaitlist_Idempotent_ReturnsExistingEntry()
    {
        var (_, service, _, location, _, type, _, patientUser) = SeedScenario();
        var request = new JoinWaitlistRequest(location.Id, type.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null);

        var first = await service.JoinWaitlistAsync(patientUser, request);
        var second = await service.JoinWaitlistAsync(patientUser, request);

        Assert.Equal(first.Id, second.Id);
    }
}

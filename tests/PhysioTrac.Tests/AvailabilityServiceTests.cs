using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors `care/availability.py`'s slot computation: working hours
/// minus existing appointments, time off, and location closures.</summary>
public class AvailabilityServiceTests
{
    private static (PhysioTracDbContext Db, AvailabilityService Service, Organization Org, Location Location, Provider Provider, AppointmentType Type)
        SeedScenario()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var location = new Location { OrganizationId = org.Id, Name = "Main Clinic", Timezone = "UTC" };
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Terry", LastName = "Therapist" };
        var appointmentType = new AppointmentType { OrganizationId = org.Id, Name = "Follow-up", DefaultDurationMinutes = 30 };
        var config = new BookingConfiguration
        {
            OrganizationId = org.Id,
            OnlineBookingEnabled = true,
            MinNoticeHours = 0,
            MaxAdvanceDays = 365,
            SlotIntervalMinutes = 30,
        };

        // Availability tomorrow 09:00-10:00 — exactly two 30-minute slots.
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var availability = new ProviderAvailability
        {
            ProviderId = provider.Id,
            LocationId = location.Id,
            DayOfWeek = tomorrow.ToWeekday(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Active = true,
        };

        db.Organizations.Add(org);
        db.Locations.Add(location);
        db.Providers.Add(provider);
        db.AppointmentTypes.Add(appointmentType);
        db.BookingConfigurations.Add(config);
        db.ProviderAvailabilities.Add(availability);
        db.SaveChanges();

        var service = new AvailabilityService(db);
        return (db, service, org, location, provider, appointmentType);
    }

    [Fact]
    public async Task GetProviderSlots_ReturnsExpectedSlotsWithinWorkingHours()
    {
        var (_, service, _, location, provider, appointmentType) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var slots = await service.GetProviderSlotsAsync(provider.Id, location.Id, appointmentType.Id, tomorrow);

        Assert.Equal(2, slots.Count);
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(slots[0].Start.UtcDateTime));
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(slots[1].Start.UtcDateTime));
    }

    [Fact]
    public async Task GetProviderSlots_ExistingAppointment_RemovesOverlappingSlot()
    {
        var (db, service, _, location, provider, appointmentType) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var patient = new Patient { OrganizationId = provider.OrganizationId, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Patients.Add(patient);
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
        db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id,
            TherapistId = Guid.NewGuid(),
            ProviderId = provider.Id,
            Status = AppointmentStatus.Scheduled,
            StartsAt = nineAm,
            EndsAt = nineAm.AddMinutes(30),
            CreatedById = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var slots = await service.GetProviderSlotsAsync(provider.Id, location.Id, appointmentType.Id, tomorrow);

        Assert.Single(slots);
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(slots[0].Start.UtcDateTime));
    }

    [Fact]
    public async Task GetProviderSlots_CancelledAppointment_DoesNotBlockSlot()
    {
        var (db, service, _, location, provider, appointmentType) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var patient = new Patient { OrganizationId = provider.OrganizationId, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Patients.Add(patient);
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
        db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id,
            TherapistId = Guid.NewGuid(),
            ProviderId = provider.Id,
            Status = AppointmentStatus.Cancelled,
            StartsAt = nineAm,
            EndsAt = nineAm.AddMinutes(30),
            CreatedById = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var slots = await service.GetProviderSlotsAsync(provider.Id, location.Id, appointmentType.Id, tomorrow);

        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public async Task GetProviderSlots_NoAvailabilityWindow_ReturnsEmpty()
    {
        var (db, service, org, _, provider, appointmentType) = SeedScenario();
        var location2 = new Location { OrganizationId = org.Id, Name = "Second Clinic", Timezone = "UTC" };
        db.Locations.Add(location2);
        await db.SaveChangesAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var slots = await service.GetProviderSlotsAsync(provider.Id, location2.Id, appointmentType.Id, tomorrow);

        Assert.Empty(slots);
    }
}

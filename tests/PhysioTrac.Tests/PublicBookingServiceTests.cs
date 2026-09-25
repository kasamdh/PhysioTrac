using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhysioTrac.Application.Booking;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors the original's `PublicBookingTests` intent: nothing is
/// trusted from the client — organization/location/provider/type ownership
/// and slot availability are all re-checked server-side.</summary>
public class PublicBookingServiceTests
{
    private static (PhysioTracDbContext Db, PublicBookingService Service, Organization Org, Location Location, Provider Provider, AppointmentType Type)
        SeedScenario(bool onlineBookingEnabled = true)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var location = new Location { OrganizationId = org.Id, Name = "Main Clinic", Timezone = "UTC", IsActive = true };
        var providerUserId = Guid.NewGuid();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Terry", LastName = "Therapist", UserId = providerUserId, IsActive = true, OnlineBookingEnabled = true };
        var appointmentType = new AppointmentType { OrganizationId = org.Id, Name = "Evaluation", DefaultDurationMinutes = 30, IsActive = true, OnlineBookingEnabled = true };
        var config = new BookingConfiguration { OrganizationId = org.Id, OnlineBookingEnabled = onlineBookingEnabled, MinNoticeHours = 0, MaxAdvanceDays = 365, SlotIntervalMinutes = 30 };

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
        var link = new ProviderAppointmentType { ProviderId = provider.Id, AppointmentTypeId = appointmentType.Id, Active = true };

        db.Organizations.Add(org);
        db.Locations.Add(location);
        db.Providers.Add(provider);
        db.AppointmentTypes.Add(appointmentType);
        db.BookingConfigurations.Add(config);
        db.ProviderAvailabilities.Add(availability);
        db.ProviderAppointmentTypes.Add(link);
        db.SaveChanges();
        provider.Locations.Add(location);
        db.SaveChanges();

        var audit = new AuditService(db);
        var availabilityService = new AvailabilityService(db);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PublicBookingService(db, availabilityService, audit, cache);
        return (db, service, org, location, provider, appointmentType);
    }

    [Fact]
    public async Task GetOrganization_UnknownSlug_ReturnsNull()
    {
        var (_, service, _, _, _, _) = SeedScenario();
        var result = await service.GetOrganizationAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrganization_OnlineBookingDisabled_ReturnsNull()
    {
        var (_, service, org, _, _, _) = SeedScenario(onlineBookingEnabled: false);
        var result = await service.GetOrganizationAsync(org.Slug);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateBooking_ValidSlot_Succeeds()
    {
        var (_, service, org, location, provider, type) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

        var result = await service.CreateBookingAsync(new PublicBookingRequest(
            org.Slug, location.Id, type.Id, provider.Id, nineAm, true,
            new PublicPatientInfo("Jane", "Doe", new DateOnly(1990, 1, 1), "jane@example.com", "555-1234", null, null), "Knee pain"), null);

        Assert.Equal(nineAm, result.StartsAt);
        Assert.StartsWith("APT-", result.ConfirmationNumber);
    }

    [Fact]
    public async Task CreateBooking_SlotAlreadyTaken_ThrowsSlotNoLongerAvailable()
    {
        var (_, service, org, location, provider, type) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
        var patientInfo = new PublicPatientInfo("Jane", "Doe", new DateOnly(1990, 1, 1), "jane@example.com", "555-1234", null, null);

        await service.CreateBookingAsync(new PublicBookingRequest(org.Slug, location.Id, type.Id, provider.Id, nineAm, true, patientInfo, null), null);

        await Assert.ThrowsAsync<SlotNoLongerAvailableException>(() =>
            service.CreateBookingAsync(new PublicBookingRequest(org.Slug, location.Id, type.Id, provider.Id, nineAm, true, patientInfo, null), null));
    }

    [Fact]
    public async Task CreateBooking_UnknownOrganizationSlug_ThrowsNotFound()
    {
        var (_, service, _, location, provider, type) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

        await Assert.ThrowsAsync<BookingNotFoundException>(() => service.CreateBookingAsync(new PublicBookingRequest(
            "no-such-org", location.Id, type.Id, provider.Id, nineAm, true,
            new PublicPatientInfo("Jane", "Doe", new DateOnly(1990, 1, 1), null, null, null, null), null), null));
    }

    // Same rationale as PortalBookingServiceTests' equivalent case:
    // GetProviderSlotsAsync has no org check of its own, so this proves the
    // organization-filtered provider/location/type lookups above it in
    // CreateBookingAsync are what actually stop a caller from pairing a
    // valid org slug with another org's provider.
    [Fact]
    public async Task CreateBooking_ProviderFromAnotherOrganization_ThrowsBookingNotFound()
    {
        var (db, service, org, location, _, type) = SeedScenario();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        var otherProvider = new Provider { OrganizationId = otherOrg.Id, FirstName = "Other", LastName = "Provider", UserId = Guid.NewGuid(), IsActive = true, OnlineBookingEnabled = true };
        db.Organizations.Add(otherOrg);
        db.Providers.Add(otherProvider);
        await db.SaveChangesAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

        await Assert.ThrowsAsync<BookingNotFoundException>(() => service.CreateBookingAsync(new PublicBookingRequest(
            org.Slug, location.Id, type.Id, otherProvider.Id, nineAm, true,
            new PublicPatientInfo("Jane", "Doe", new DateOnly(1990, 1, 1), null, null, null, null), null), null));
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task GetAvailability_ExceedingRateLimit_ThrowsRateLimited()
    {
        var (_, service, org, location, _, type) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        for (var i = 0; i < 60; i++)
        {
            await service.GetAvailabilityAsync(org.Slug, location.Id, type.Id, tomorrow, null, "1.2.3.4");
        }

        await Assert.ThrowsAsync<RateLimitedException>(() =>
            service.GetAvailabilityAsync(org.Slug, location.Id, type.Id, tomorrow, null, "1.2.3.4"));
    }

    [Fact]
    public async Task ReturningPatient_MatchingEmailNameAndDob_ReusesExistingChart()
    {
        var (db, service, org, location, provider, type) = SeedScenario();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var nineAm = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
        var existingPatient = new Patient
        {
            OrganizationId = org.Id,
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Email = "jane@example.com",
        };
        db.Patients.Add(existingPatient);
        await db.SaveChangesAsync();

        var result = await service.CreateBookingAsync(new PublicBookingRequest(
            org.Slug, location.Id, type.Id, provider.Id, nineAm, false,
            new PublicPatientInfo("Jane", "Doe", new DateOnly(1990, 1, 1), "jane@example.com", "555-9999", null, null), null), null);

        var patientCount = await db.Patients.CountAsync(p => p.OrganizationId == org.Id);
        Assert.Equal(1, patientCount);
        var appointment = await db.Appointments.FirstAsync(a => a.Id == result.AppointmentId);
        Assert.Equal(existingPatient.Id, appointment.PatientId);
    }
}

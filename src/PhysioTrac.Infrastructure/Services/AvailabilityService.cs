using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `care/availability.py`.</summary>
public class AvailabilityService : IAvailabilityService
{
    private readonly PhysioTracDbContext _db;

    public AvailabilityService(PhysioTracDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Slot>> GetProviderSlotsAsync(
        Guid providerId, Guid locationId, Guid appointmentTypeId, DateOnly onDate,
        Guid? excludeAppointmentId = null, CancellationToken ct = default)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct)
            ?? throw new NotFoundException("Provider was not found.");
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct)
            ?? throw new NotFoundException("Location was not found.");
        var appointmentType = await _db.AppointmentTypes.FirstOrDefaultAsync(a => a.Id == appointmentTypeId, ct)
            ?? throw new NotFoundException("Appointment type was not found.");
        var config = await GetOrCreateConfigAsync(provider.OrganizationId, ct);

        return await ComputeSlotsAsync(provider, location, appointmentType, onDate, config, excludeAppointmentId, ct);
    }

    public async Task<IReadOnlyList<ProviderSlotsDto>> GetAvailableSlotsAsync(
        Guid organizationId, Guid locationId, Guid appointmentTypeId, DateOnly onDate,
        Guid? providerId = null, Guid? excludeAppointmentId = null, CancellationToken ct = default)
    {
        var config = await GetOrCreateConfigAsync(organizationId, ct);
        if (!config.OnlineBookingEnabled)
        {
            return Array.Empty<ProviderSlotsDto>();
        }

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct)
            ?? throw new NotFoundException("Location was not found.");
        var appointmentType = await _db.AppointmentTypes.FirstOrDefaultAsync(a => a.Id == appointmentTypeId, ct)
            ?? throw new NotFoundException("Appointment type was not found.");

        IReadOnlyList<Provider> providers;
        if (providerId is Guid pid)
        {
            var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == pid, ct);
            providers = provider is null ? new List<Provider>() : new List<Provider> { provider };
        }
        else
        {
            providers = await EligibleProvidersAsync(organizationId, locationId, appointmentTypeId, ct);
        }

        var results = new List<ProviderSlotsDto>();
        foreach (var provider in providers)
        {
            var slots = await ComputeSlotsAsync(provider, location, appointmentType, onDate, config, excludeAppointmentId, ct);
            if (slots.Count > 0)
            {
                results.Add(new ProviderSlotsDto(provider.Id, provider.FullName, slots));
            }
        }
        return results;
    }

    public async Task<IReadOnlyList<Provider>> EligibleProvidersAsync(Guid organizationId, Guid locationId, Guid appointmentTypeId, CancellationToken ct = default)
    {
        return await _db.Providers
            .Where(p => p.OrganizationId == organizationId && p.IsActive && p.OnlineBookingEnabled && p.UserId != null)
            .Where(p => p.Locations.Any(l => l.Id == locationId))
            .Where(p => p.AppointmentTypeLinks.Any(link => link.AppointmentTypeId == appointmentTypeId && link.Active))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(ct);
    }

    private async Task<BookingConfiguration> GetOrCreateConfigAsync(Guid organizationId, CancellationToken ct)
    {
        var config = await _db.BookingConfigurations.FirstOrDefaultAsync(c => c.OrganizationId == organizationId, ct);
        if (config is null)
        {
            config = new BookingConfiguration { OrganizationId = organizationId };
            _db.BookingConfigurations.Add(config);
            await _db.SaveChangesAsync(ct);
        }
        return config;
    }

    private async Task<(int Duration, int BufferBefore, int BufferAfter)> DurationMinutesAsync(Provider provider, AppointmentType appointmentType, CancellationToken ct)
    {
        var link = await _db.ProviderAppointmentTypes.FirstOrDefaultAsync(
            l => l.ProviderId == provider.Id && l.AppointmentTypeId == appointmentType.Id && l.Active, ct);
        var duration = link?.CustomDurationMinutes ?? appointmentType.DefaultDurationMinutes;
        return (duration, appointmentType.BufferBeforeMinutes, appointmentType.BufferAfterMinutes);
    }

    private async Task<IReadOnlyList<Slot>> ComputeSlotsAsync(
        Provider provider, Domain.Entities.Location location, AppointmentType appointmentType, DateOnly onDate,
        BookingConfiguration config, Guid? excludeAppointmentId, CancellationToken ct)
    {
        var (durationMinutes, bufferBefore, bufferAfter) = await DurationMinutesAsync(provider, appointmentType, ct);
        var totalSpan = TimeSpan.FromMinutes(durationMinutes + bufferBefore + bufferAfter);
        var interval = TimeSpan.FromMinutes(config.SlotIntervalMinutes);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(location.Timezone);
        var weekday = onDate.ToWeekday();

        var windows = await _db.ProviderAvailabilities
            .Where(a => a.ProviderId == provider.Id && a.LocationId == location.Id && a.DayOfWeek == weekday && a.Active)
            .Where(a => a.EffectiveFrom == null || a.EffectiveFrom <= onDate)
            .Where(a => a.EffectiveUntil == null || a.EffectiveUntil >= onDate)
            .ToListAsync(ct);

        if (windows.Count == 0)
        {
            return Array.Empty<Slot>();
        }

        var dayStartLocal = onDate.ToDateTime(TimeOnly.MinValue);
        var dayEndLocal = onDate.ToDateTime(TimeOnly.MaxValue);
        var dayStart = new DateTimeOffset(dayStartLocal, tz.GetUtcOffset(dayStartLocal));
        var dayEnd = new DateTimeOffset(dayEndLocal, tz.GetUtcOffset(dayEndLocal));

        var busy = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        var appointments = await _db.Appointments
            .Where(a => a.ProviderId == provider.Id && a.StartsAt < dayEnd && a.EndsAt > dayStart)
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .ToListAsync(ct);
        foreach (var appt in appointments)
        {
            if (excludeAppointmentId is not null && appt.Id == excludeAppointmentId) continue;
            busy.Add((appt.StartsAt, appt.EndsAt));
        }

        var timeOff = await _db.ProviderTimeOffs
            .Where(t => t.ProviderId == provider.Id && t.Status == TimeOffStatus.Approved && t.StartDateTime < dayEnd && t.EndDateTime > dayStart)
            .ToListAsync(ct);
        busy.AddRange(timeOff.Select(t => (t.StartDateTime, t.EndDateTime)));

        var closures = await _db.LocationClosures
            .Where(c => c.LocationId == location.Id && c.Active && c.StartDateTime < dayEnd && c.EndDateTime > dayStart)
            .ToListAsync(ct);
        busy.AddRange(closures.Select(c => (c.StartDateTime, c.EndDateTime)));

        var now = DateTimeOffset.UtcNow;
        var earliestAllowed = now.AddHours(config.MinNoticeHours);
        var latestAllowed = now.AddDays(config.MaxAdvanceDays);

        var slots = new List<Slot>();
        foreach (var window in windows)
        {
            var windowStartLocal = onDate.ToDateTime(window.StartTime);
            var windowEndLocal = onDate.ToDateTime(window.EndTime);
            var windowStart = new DateTimeOffset(windowStartLocal, tz.GetUtcOffset(windowStartLocal));
            var windowEnd = new DateTimeOffset(windowEndLocal, tz.GetUtcOffset(windowEndLocal));

            var cursor = windowStart;
            while (cursor + totalSpan <= windowEnd)
            {
                var candidateStart = cursor + TimeSpan.FromMinutes(bufferBefore);
                var candidateEnd = candidateStart + TimeSpan.FromMinutes(durationMinutes);
                var spanStart = candidateStart - TimeSpan.FromMinutes(bufferBefore);
                var spanEnd = candidateEnd + TimeSpan.FromMinutes(bufferAfter);

                var overlapsBusy = busy.Any(b => spanStart < b.End && spanEnd > b.Start);
                var withinNotice = candidateStart >= earliestAllowed;
                var withinAdvanceLimit = candidateStart <= latestAllowed;

                if (!overlapsBusy && withinNotice && withinAdvanceLimit)
                {
                    slots.Add(new Slot(candidateStart, candidateEnd));
                }

                cursor += interval;
            }
        }

        return slots.OrderBy(s => s.Start).ToList();
    }
}

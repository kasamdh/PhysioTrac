using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>See <see cref="IProviderAvailabilityService"/>.</summary>
public class ProviderAvailabilityService : IProviderAvailabilityService
{
    private const int MaxTimeOffBlocks = 366;

    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public ProviderAvailabilityService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<ProviderScheduleDto> GetAsync(Guid providerId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        var (organization, provider) = await LoadAsync(providerId, actor, ct);
        return await ToDtoAsync(organization, provider, actor, ct);
    }

    public async Task<ProviderScheduleDto> ReplaceWeeklyHoursAsync(
        Guid providerId, ReplaceWeeklyHoursRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AvailabilityManagement);
        var (organization, provider) = await LoadAsync(providerId, actor, ct);

        var windows = new List<(WeeklyHoursWindowRequest Request, TimeOnly Start, TimeOnly End)>();
        foreach (var w in request.Windows ?? [])
        {
            var start = ParseTime(w.Start);
            var end = ParseTime(w.End);
            if (end <= start)
            {
                throw new InvalidOperationException($"{w.DayOfWeek}: the end time must be after the start time.");
            }
            if (!provider.Locations.Any(l => l.Id == w.LocationId))
            {
                throw new InvalidOperationException($"{w.DayOfWeek}: {provider.FullName} isn't assigned to that location.");
            }
            if (w.EffectiveFrom is DateOnly from && w.EffectiveUntil is DateOnly until && until < from)
            {
                throw new InvalidOperationException($"{w.DayOfWeek}: 'effective until' must be on or after 'effective from'.");
            }
            windows.Add((w, start, end));
        }

        // A provider can't be in two places at once: no overlapping hours on
        // the same weekday while both windows are in effect.
        for (var i = 0; i < windows.Count; i++)
        {
            for (var j = i + 1; j < windows.Count; j++)
            {
                var (a, b) = (windows[i], windows[j]);
                if (a.Request.DayOfWeek == b.Request.DayOfWeek && a.Start < b.End && b.Start < a.End && EffectiveRangesOverlap(a.Request, b.Request))
                {
                    throw new InvalidOperationException($"{a.Request.DayOfWeek}: {a.Start:HH\\:mm}–{a.End:HH\\:mm} overlaps {b.Start:HH\\:mm}–{b.End:HH\\:mm}.");
                }
            }
        }

        var existing = await _db.ProviderAvailabilities.Where(a => a.ProviderId == provider.Id).ToListAsync(ct);
        _db.ProviderAvailabilities.RemoveRange(existing);
        _db.ProviderAvailabilities.AddRange(windows.Select(w => new ProviderAvailability
        {
            ProviderId = provider.Id,
            LocationId = w.Request.LocationId,
            DayOfWeek = w.Request.DayOfWeek,
            StartTime = w.Start,
            EndTime = w.End,
            EffectiveFrom = w.Request.EffectiveFrom,
            EffectiveUntil = w.Request.EffectiveUntil,
        }));
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "provider.weekly_hours_updated", nameof(Provider), provider.Id, organization.Id,
            metadata: new { previousWindows = existing.Count, windows = windows.Count }, ct: ct);

        return await ToDtoAsync(organization, provider, actor, ct);
    }

    public async Task<CreateTimeOffResultDto> CreateTimeOffAsync(
        Guid providerId, CreateTimeOffRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AvailabilityManagement);
        var (organization, provider) = await LoadAsync(providerId, actor, ct);
        if (request.EndsAt <= request.StartsAt)
        {
            throw new InvalidOperationException("The end must be after the start.");
        }

        var tz = TimeZoneFor(organization, provider);
        var blocks = Expand(request, tz);
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()[..Math.Min(request.Notes.Trim().Length, 500)];
        var created = blocks.Select(b => new ProviderTimeOff
        {
            ProviderId = provider.Id,
            StartDateTime = b.Start,
            EndDateTime = b.End,
            Reason = request.Reason,
            Notes = notes,
            Status = TimeOffStatus.Approved,
        }).ToList();
        _db.ProviderTimeOffs.AddRange(created);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "provider.time_off_created", nameof(Provider), provider.Id, organization.Id,
            metadata: new
            {
                reason = request.Reason.ToString(),
                blocks = created.Count,
                firstStart = created[0].StartDateTime,
                lastEnd = created[^1].EndDateTime,
            }, ct: ct);

        // Existing visits aren't moved automatically -- that's a judgment call
        // per patient -- but staff must see which ones are now in conflict.
        var first = created[0].StartDateTime;
        var last = created[^1].EndDateTime;
        var candidates = await _db.Appointments
            .Where(a => (a.ProviderId == provider.Id || (provider.UserId != null && a.TherapistId == provider.UserId)) &&
                        a.StartsAt < last && a.EndsAt > first &&
                        a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow && a.Status != AppointmentStatus.Completed)
            .ToListAsync(ct);
        var affected = candidates
            .Where(a => created.Any(t => a.StartsAt < t.EndDateTime && a.EndsAt > t.StartDateTime))
            .OrderBy(a => a.StartsAt)
            .ToList();
        var patientIds = affected.Select(a => a.PatientId).Distinct().ToList();
        var names = await _db.Patients.Where(p => patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.FirstName + " " + p.LastName).Trim(), ct);

        return new CreateTimeOffResultDto(
            created.Select(ToDto).ToList(),
            affected.Select(a => new AffectedAppointmentDto(a.Id, names.GetValueOrDefault(a.PatientId, "Patient"), a.StartsAt, a.EndsAt)).ToList());
    }

    public async Task CancelTimeOffAsync(Guid providerId, Guid timeOffId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AvailabilityManagement);
        var (organization, provider) = await LoadAsync(providerId, actor, ct);
        var timeOff = await _db.ProviderTimeOffs.FirstOrDefaultAsync(t => t.Id == timeOffId && t.ProviderId == provider.Id, ct)
            ?? throw new NotFoundException("Time off was not found.");
        if (timeOff.Status == TimeOffStatus.Cancelled) return;

        timeOff.Status = TimeOffStatus.Cancelled;
        timeOff.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "provider.time_off_cancelled", nameof(Provider), provider.Id, organization.Id,
            metadata: new { timeOffId, reason = timeOff.Reason.ToString(), timeOff.StartDateTime, timeOff.EndDateTime }, ct: ct);
    }

    // ---- helpers ----

    private async Task<(Organization Organization, Provider Provider)> LoadAsync(Guid providerId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var provider = await _db.Providers.Include(p => p.Locations)
            .FirstOrDefaultAsync(p => p.Id == providerId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Provider was not found.");
        return (organization, provider);
    }

    private async Task<ProviderScheduleDto> ToDtoAsync(Organization organization, Provider provider, ICurrentUser actor, CancellationToken ct)
    {
        var locationNames = provider.Locations.ToDictionary(l => l.Id, l => l.Name);
        var windows = await _db.ProviderAvailabilities.Where(a => a.ProviderId == provider.Id && a.Active).ToListAsync(ct);
        // Current and upcoming only -- past lunches are just noise here.
        var now = DateTimeOffset.UtcNow;
        var timeOff = await _db.ProviderTimeOffs
            .Where(t => t.ProviderId == provider.Id && t.Status == TimeOffStatus.Approved && t.EndDateTime > now)
            .ToListAsync(ct);

        return new ProviderScheduleDto(
            provider.Id, provider.FullName, provider.Credentials, TimeZoneFor(organization, provider).Id,
            RoleSets.AvailabilityManagement.Contains(actor.Role),
            provider.Locations.OrderBy(l => l.Name).Select(l => new ScheduleLocationDto(l.Id, l.Name, l.Timezone, l.State)).ToList(),
            windows.OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime)
                .Select(w => new WeeklyHoursWindowDto(w.Id, w.DayOfWeek, w.LocationId, locationNames.GetValueOrDefault(w.LocationId, "—"),
                    w.StartTime.ToString("HH:mm"), w.EndTime.ToString("HH:mm"), w.EffectiveFrom, w.EffectiveUntil))
                .ToList(),
            timeOff.OrderBy(t => t.StartDateTime).Take(500).Select(ToDto).ToList());
    }

    private static TimeOffDto ToDto(ProviderTimeOff t) =>
        new(t.Id, t.StartDateTime, t.EndDateTime, t.Reason, t.Notes, t.LocationId, t.Status);

    /// <summary>The provider's clinic timezone: their first location's, else
    /// the organization's -- the same fallback the Day view uses.</summary>
    private static TimeZoneInfo TimeZoneFor(Organization organization, Provider provider)
    {
        var id = provider.Locations.OrderBy(l => l.Name).Select(l => l.Timezone).FirstOrDefault() ?? organization.Timezone;
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Utc;
    }

    /// <summary>One block, or one per matching weekday through RepeatUntil,
    /// each at the first block's clinic-local times (DST-safe).</summary>
    private static List<(DateTimeOffset Start, DateTimeOffset End)> Expand(CreateTimeOffRequest request, TimeZoneInfo tz)
    {
        if (request.RepeatUntil is not DateOnly until)
        {
            return [(request.StartsAt, request.EndsAt)];
        }

        var startLocal = TimeZoneInfo.ConvertTime(request.StartsAt, tz);
        var firstDate = DateOnly.FromDateTime(startLocal.DateTime);
        if (until < firstDate)
        {
            throw new InvalidOperationException("'Repeat until' must be on or after the first day.");
        }
        var startTime = TimeOnly.FromDateTime(startLocal.DateTime);
        var length = request.EndsAt - request.StartsAt;
        var days = request.RepeatDays is { Count: > 0 } ? request.RepeatDays.ToHashSet() : [firstDate.ToWeekday()];

        var blocks = new List<(DateTimeOffset, DateTimeOffset)>();
        for (var date = firstDate; date <= until; date = date.AddDays(1))
        {
            if (!days.Contains(date.ToWeekday())) continue;
            if (blocks.Count == MaxTimeOffBlocks)
            {
                throw new InvalidOperationException($"That would create more than {MaxTimeOffBlocks} blocks -- choose an earlier 'repeat until' date.");
            }
            var local = date.ToDateTime(startTime);
            var start = new DateTimeOffset(local, tz.GetUtcOffset(local));
            blocks.Add((start, start + length));
        }
        if (blocks.Count == 0)
        {
            throw new InvalidOperationException("None of the chosen days fall in that range.");
        }
        return blocks;
    }

    private static bool EffectiveRangesOverlap(WeeklyHoursWindowRequest a, WeeklyHoursWindowRequest b) =>
        (a.EffectiveFrom ?? DateOnly.MinValue) <= (b.EffectiveUntil ?? DateOnly.MaxValue) &&
        (b.EffectiveFrom ?? DateOnly.MinValue) <= (a.EffectiveUntil ?? DateOnly.MaxValue);

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : throw new InvalidOperationException($"'{value}' isn't a valid time (use HH:mm).");
}

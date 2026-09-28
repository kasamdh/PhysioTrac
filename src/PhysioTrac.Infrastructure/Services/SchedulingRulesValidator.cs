using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Everything a proposed appointment slot is checked against.
/// All ids are assumed already verified as the caller's own organization's
/// (AppointmentService.RequireOwnOrgReferencesAsync) -- this only answers
/// "may it go here", not "may this caller see these rows".</summary>
public sealed record SlotCheck(
    Guid OrganizationId, Guid PatientId, Guid TherapistId, Guid? ProviderId, Guid? LocationId, Guid? RoomId,
    Guid? AppointmentTypeId, AppointmentKind Kind, DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? ExcludeAppointmentId);

/// <summary>The single set of scheduling rules every staff booking path
/// (create, series, reschedule/drag-and-drop, and the move dry run) goes
/// through, so the calendar and the API can never disagree about what's
/// bookable. Returns every violation rather than stopping at the first, so
/// the UI can show the whole picture in one conflict dialog.
///
/// "Not configured" rules are skipped rather than failed: a provider with no
/// weekly availability rows, or no appointment-type restrictions, is treated
/// as unrestricted -- otherwise every existing organization that never set
/// those up would be unable to book anything. License checks are NOT
/// skippable that way: a provider with no active, unexpired license can't
/// be booked.</summary>
public class SchedulingRulesValidator
{
    private static readonly AppointmentKind[] PtOnlyKinds =
        [AppointmentKind.Evaluation, AppointmentKind.ReEvaluation, AppointmentKind.Discharge];

    private readonly PhysioTracDbContext _db;

    public SchedulingRulesValidator(PhysioTracDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SchedulingViolation>> CheckAsync(SlotCheck slot, CancellationToken ct)
    {
        var violations = new List<SchedulingViolation>();

        var location = slot.LocationId is Guid lid ? await _db.Locations.FirstOrDefaultAsync(l => l.Id == lid, ct) : null;
        var tz = await TimeZoneForAsync(slot.OrganizationId, location, ct);
        var startLocal = TimeZoneInfo.ConvertTime(slot.StartsAt, tz);
        var endLocal = TimeZoneInfo.ConvertTime(slot.EndsAt, tz);
        var serviceDate = DateOnly.FromDateTime(startLocal.DateTime);
        string Time(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, tz).ToString("h:mm tt");

        Provider? provider = null;
        if (slot.ProviderId is Guid pid)
        {
            provider = await _db.Providers
                .Include(p => p.Locations)
                .Include(p => p.Licenses)
                .Include(p => p.AppointmentTypeLinks)
                .FirstAsync(p => p.Id == pid, ct);
            await CheckProviderAsync(provider, location, slot, serviceDate, startLocal, endLocal, Time, violations, ct);
        }

        if (location is not null && await _db.LocationClosures.AnyAsync(c =>
                c.LocationId == location.Id && c.Active && c.StartDateTime < slot.EndsAt && c.EndDateTime > slot.StartsAt, ct))
        {
            violations.Add(new(SchedulingViolationCodes.LocationClosed, $"{location.Name} is closed during this time.", true));
        }

        await CheckConflictsAsync(slot, provider, Time, violations, ct);
        return violations;
    }

    private async Task CheckProviderAsync(
        Provider provider, Location? location, SlotCheck slot, DateOnly serviceDate,
        DateTimeOffset startLocal, DateTimeOffset endLocal, Func<DateTimeOffset, string> time,
        List<SchedulingViolation> violations, CancellationToken ct)
    {
        var name = provider.FullName;

        if (!provider.IsActive)
        {
            violations.Add(new(SchedulingViolationCodes.ProviderInactive, $"{name} is inactive and can't take appointments.", false));
        }

        if (location is not null && !provider.Locations.Any(l => l.Id == location.Id))
        {
            violations.Add(new(SchedulingViolationCodes.ProviderNotAtLocation, $"{name} doesn't work at {location.Name}.", false));
        }

        // Licensed for the location's state on the date of service; with no
        // location (or a location with no state on file), any valid license.
        var state = location?.State;
        var validLicense = provider.Licenses.Any(l =>
            l.Status == ProviderLicenseStatus.Active && l.ExpirationDate >= serviceDate &&
            (string.IsNullOrWhiteSpace(state) || string.Equals(l.State, state, StringComparison.OrdinalIgnoreCase)));
        if (!validLicense)
        {
            violations.Add(new(SchedulingViolationCodes.LicenseInvalid, string.IsNullOrWhiteSpace(state)
                ? $"{name} has no active, unexpired license on {serviceDate:MMM d, yyyy}."
                : $"{name} has no active, unexpired {state.ToUpperInvariant()} license on {serviceDate:MMM d, yyyy}.", false));
        }

        AppointmentType? appointmentType = null;
        if (slot.AppointmentTypeId is Guid atid)
        {
            appointmentType = await _db.AppointmentTypes.FirstOrDefaultAsync(t => t.Id == atid, ct);
            var activeLinks = provider.AppointmentTypeLinks.Where(l => l.Active).ToList();
            if (activeLinks.Count > 0 && !activeLinks.Any(l => l.AppointmentTypeId == atid))
            {
                violations.Add(new(SchedulingViolationCodes.AppointmentTypeNotAllowed,
                    $"{name} isn't set up to see {appointmentType?.Name ?? "this appointment type"} appointments.", false));
            }
        }

        if (provider.Discipline == ProviderDiscipline.PTA &&
            (PtOnlyKinds.Contains(slot.Kind) || appointmentType?.DefaultKind is AppointmentKind k && PtOnlyKinds.Contains(k)))
        {
            violations.Add(new(SchedulingViolationCodes.PtaScope,
                $"{name} is a PTA and can't perform an evaluation, re-evaluation, or discharge visit.", false));
        }

        await CheckAvailabilityAsync(provider, location, slot, serviceDate, startLocal, endLocal, time, violations, ct);
    }

    private async Task CheckAvailabilityAsync(
        Provider provider, Location? location, SlotCheck slot, DateOnly serviceDate,
        DateTimeOffset startLocal, DateTimeOffset endLocal, Func<DateTimeOffset, string> time,
        List<SchedulingViolation> violations, CancellationToken ct)
    {
        var name = provider.FullName;
        var windows = await _db.ProviderAvailabilities.Where(a => a.ProviderId == provider.Id && a.Active).ToListAsync(ct);
        if (windows.Count > 0)
        {
            var weekday = serviceDate.ToWeekday();
            var startTime = TimeOnly.FromTimeSpan(startLocal.TimeOfDay);
            var endTime = TimeOnly.FromTimeSpan(endLocal.TimeOfDay);
            var sameDay = DateOnly.FromDateTime(endLocal.DateTime) == serviceDate
                || (DateOnly.FromDateTime(endLocal.DateTime) == serviceDate.AddDays(1) && endTime == TimeOnly.MinValue);
            if (endTime == TimeOnly.MinValue) endTime = TimeOnly.MaxValue;

            var fits = sameDay && windows.Any(w =>
                w.DayOfWeek == weekday &&
                (location is null || w.LocationId == location.Id) &&
                (w.EffectiveFrom is null || w.EffectiveFrom <= serviceDate) &&
                (w.EffectiveUntil is null || w.EffectiveUntil >= serviceDate) &&
                w.StartTime <= startTime && endTime <= w.EndTime);
            if (!fits)
            {
                violations.Add(new(SchedulingViolationCodes.OutsideWorkingHours, location is null
                    ? $"{name} isn't scheduled to work from {time(slot.StartsAt)} to {time(slot.EndsAt)} on {serviceDate:dddd}."
                    : $"{name} isn't scheduled to work at {location.Name} from {time(slot.StartsAt)} to {time(slot.EndsAt)} on {serviceDate:dddd}.", true));
            }
        }

        var timeOff = await _db.ProviderTimeOffs
            .Where(t => t.ProviderId == provider.Id && t.Status == TimeOffStatus.Approved &&
                        t.StartDateTime < slot.EndsAt && t.EndDateTime > slot.StartsAt)
            .OrderBy(t => t.StartDateTime)
            .FirstOrDefaultAsync(ct);
        if (timeOff is not null)
        {
            violations.Add(new(SchedulingViolationCodes.TimeOff,
                $"{name} is unavailable ({timeOff.Reason.ToString().ToLowerInvariant()}) from {time(timeOff.StartDateTime)} to {time(timeOff.EndDateTime)}.", true));
        }
    }

    /// <summary>The original provider/therapist/patient/room overlap check,
    /// now naming who's busy and when. Only double-booking a clinician is
    /// overridable; a patient can't be in two places, and neither can a room.</summary>
    private async Task CheckConflictsAsync(SlotCheck slot, Provider? provider, Func<DateTimeOffset, string> time,
        List<SchedulingViolation> violations, CancellationToken ct)
    {
        var excludeId = slot.ExcludeAppointmentId ?? Guid.Empty;
        var overlapping = _db.Appointments.Where(a =>
            a.Id != excludeId &&
            a.StartsAt < slot.EndsAt && a.EndsAt > slot.StartsAt &&
            a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow);

        var clinicianBusy = await overlapping
            .Where(a => a.TherapistId == slot.TherapistId || (slot.ProviderId != null && a.ProviderId == slot.ProviderId))
            .OrderBy(a => a.StartsAt).FirstOrDefaultAsync(ct);
        if (clinicianBusy is not null)
        {
            var who = clinicianBusy.ProviderId == slot.ProviderId && provider is not null ? provider.FullName
                : clinicianBusy.TherapistId == slot.TherapistId ? "This therapist" : "This provider";
            violations.Add(new(SchedulingViolationCodes.DoubleBooked,
                $"{who} already has an appointment from {time(clinicianBusy.StartsAt)} to {time(clinicianBusy.EndsAt)}.", true));
        }

        var patientBusy = await overlapping.Where(a => a.PatientId == slot.PatientId).OrderBy(a => a.StartsAt).FirstOrDefaultAsync(ct);
        if (patientBusy is not null)
        {
            violations.Add(new(SchedulingViolationCodes.PatientConflict,
                $"This patient already has another appointment from {time(patientBusy.StartsAt)} to {time(patientBusy.EndsAt)}.", false));
        }

        if (slot.RoomId is Guid rid && await overlapping.AnyAsync(a => a.RoomId == rid, ct))
        {
            violations.Add(new(SchedulingViolationCodes.RoomConflict, "This room is already booked during this time.", false));
        }
    }

    /// <summary>Clinic-local time for a slot: the location's timezone when
    /// there is one, else the organization's.</summary>
    public async Task<TimeZoneInfo> TimeZoneForAsync(Guid organizationId, Location? location, CancellationToken ct)
    {
        var id = location?.Timezone;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = await _db.Organizations.Where(o => o.Id == organizationId).Select(o => o.Timezone).FirstOrDefaultAsync(ct);
        }
        return TimeZoneInfo.TryFindSystemTimeZoneById(id ?? "UTC", out var tz) ? tz : TimeZoneInfo.Utc;
    }
}

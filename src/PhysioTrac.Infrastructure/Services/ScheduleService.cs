using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Domain.Scheduling;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>See <see cref="IScheduleService"/>. Queries load plain rows and
/// resolve display names in memory from small per-request lookups, rather
/// than one large projection -- enum/DateOnly handling inside a Select
/// doesn't translate reliably through EF Core (same reasoning as
/// PatientsController.Timeline).</summary>
public class ScheduleService : IScheduleService
{
    private static readonly int[] AllowedSlotMinutes = [15, 30];

    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public ScheduleService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<ScheduleSettingsDto> GetSettingsAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await RequireStaffAsync(actor, ct);
        var config = await ConfigAsync(organization.Id, ct);

        var canSchedule = RoleSets.Scheduling.Contains(actor.Role);
        var isClinician = AccessControl.Enabled && actor.Role is UserRole.Therapist or UserRole.Assistant;
        var locations = await _db.Locations.Where(l => l.OrganizationId == organization.Id && l.IsActive)
            .OrderBy(l => l.Name).ToListAsync(ct);
        var types = await _db.AppointmentTypes.Where(t => t.OrganizationId == organization.Id && t.IsActive)
            .OrderBy(t => t.Name).ToListAsync(ct);

        return new ScheduleSettingsDto(
            organization.Timezone, SlotMinutes(config),
            CanCreate: canSchedule,
            CanReschedule: canSchedule && (!isClinician || config.TherapistsMayReschedule),
            CanOverride: RoleSets.ScheduleOverride.Contains(actor.Role),
            config.AllowDoubleBookOverride,
            CanManageAvailability: RoleSets.AvailabilityManagement.Contains(actor.Role),
            locations.Select(l => new ScheduleLocationDto(l.Id, l.Name, l.Timezone, l.State)).ToList(),
            (await OrganizationProviders(organization.Id).Include(p => p.Locations).OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync(ct))
                .Select(ToProviderDto).ToList(),
            types.Select(t => new ScheduleAppointmentTypeDto(t.Id, t.Name, t.DefaultDurationMinutes, t.Color, t.DefaultKind)).ToList());
    }

    public async Task<ScheduleRangeDto> GetRangeAsync(ICurrentUser actor, ScheduleQuery query, CancellationToken ct = default)
    {
        var organization = await RequireStaffAsync(actor, ct);
        RequireSaneRange(query.From, query.To, maxDays: 62);
        var tz = await TimeZoneAsync(organization, query.LocationId, ct);

        var appointments = SortByStart(await Filtered(organization.Id, query).ToListAsync(ct));
        return new ScheduleRangeDto(tz.Id, await ToDtosAsync(appointments, ct));
    }

    public async Task<ScheduleDayDto> GetDayAsync(ICurrentUser actor, DateOnly date, Guid? locationId, Guid? providerId, CancellationToken ct = default)
    {
        var organization = await RequireStaffAsync(actor, ct);
        var config = await ConfigAsync(organization.Id, ct);
        var tz = await TimeZoneAsync(organization, locationId, ct);
        var (dayStart, dayEnd) = DayBounds(date, tz);

        var appointments = SortByStart(await Filtered(organization.Id, new ScheduleQuery(dayStart, dayEnd, providerId, locationId))
            .ToListAsync(ct));

        // Columns: every active provider working at this location (or at all,
        // with no location filter), plus anyone who has an appointment today
        // even if they'd otherwise be filtered out -- an appointment must
        // never silently vanish from the Day view.
        // A provider with no login can't be assigned appointments (every
        // booking needs a TherapistId), so an empty column for them is noise.
        var providersQuery = OrganizationProviders(organization.Id).Where(p => p.IsActive && p.UserId != null);
        if (locationId is Guid lid) providersQuery = providersQuery.Where(p => p.Locations.Any(l => l.Id == lid));
        if (providerId is Guid pid) providersQuery = providersQuery.Where(p => p.Id == pid);
        var providers = await providersQuery.Include(p => p.Locations).ToListAsync(ct);
        var withAppointments = appointments.Select(a => a.ProviderId).OfType<Guid>().Except(providers.Select(p => p.Id)).ToList();
        if (withAppointments.Count > 0)
        {
            providers.AddRange(await _db.Providers.Include(p => p.Locations)
                .Where(p => withAppointments.Contains(p.Id) && p.OrganizationId == organization.Id).ToListAsync(ct));
        }
        providers = providers.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();

        var providerIds = providers.Select(p => p.Id).ToList();
        var weekday = date.ToWeekday();
        var windows = (await _db.ProviderAvailabilities.Include(a => a.Location)
                .Where(a => providerIds.Contains(a.ProviderId) && a.Active && a.DayOfWeek == weekday)
                .ToListAsync(ct))
            .Where(a => (a.EffectiveFrom is null || a.EffectiveFrom <= date) && (a.EffectiveUntil is null || a.EffectiveUntil >= date))
            .Where(a => locationId is null || a.LocationId == locationId)
            .ToList();
        var timeOff = await _db.ProviderTimeOffs
            .Where(t => providerIds.Contains(t.ProviderId) && t.Status == TimeOffStatus.Approved && t.StartDateTime < dayEnd && t.EndDateTime > dayStart)
            .ToListAsync(ct);
        var windowLocationIds = windows.Select(w => w.LocationId).Distinct().ToList();
        var closures = await _db.LocationClosures
            .Where(c => windowLocationIds.Contains(c.LocationId) && c.Active && c.StartDateTime < dayEnd && c.EndDateTime > dayStart)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var columns = providers.Select(provider =>
        {
            var own = windows.Where(w => w.ProviderId == provider.Id).OrderBy(w => w.StartTime).ToList();
            var ownLocations = own.Select(w => w.LocationId).ToHashSet();
            var blocks = timeOff.Where(t => t.ProviderId == provider.Id)
                .Select(t => new ScheduleBlockDto(t.StartDateTime, t.EndDateTime, "TimeOff", t.Reason.ToString()))
                .Concat(closures.Where(c => ownLocations.Contains(c.LocationId))
                    .Select(c => new ScheduleBlockDto(c.StartDateTime, c.EndDateTime, "Closure", c.Reason)))
                .OrderBy(b => b.StartsAt).ToList();
            var summary = Summarize(appointments.Where(a => a.ProviderId == provider.Id).ToList(), own, blocks, date, tz, now);
            return new ProviderDayColumnDto(
                ToProviderDto(provider),
                own.Select(w => new TimeWindowDto(w.StartTime.ToString("HH:mm"), w.EndTime.ToString("HH:mm"), w.LocationId, w.Location?.Name)).ToList(),
                blocks, summary);
        }).ToList();

        return new ScheduleDayDto(date, tz.Id, SlotMinutes(config), columns, await ToDtosAsync(appointments, ct));
    }

    public async Task<IReadOnlyList<ScheduleDayCountDto>> GetCountsAsync(
        ICurrentUser actor, DateOnly from, DateOnly to, Guid? locationId, Guid? providerId, CancellationToken ct = default)
    {
        var organization = await RequireStaffAsync(actor, ct);
        if (to < from || to.DayNumber - from.DayNumber > 400)
        {
            throw new InvalidOperationException("The date range must be between 1 and 400 days.");
        }
        var tz = await TimeZoneAsync(organization, locationId, ct);
        var (rangeStart, _) = DayBounds(from, tz);
        var (_, rangeEnd) = DayBounds(to, tz);

        var starts = await Filtered(organization.Id, new ScheduleQuery(rangeStart, rangeEnd, providerId, locationId))
            .Where(a => a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.StartsAt)
            .ToListAsync(ct);

        return starts
            .GroupBy(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s, tz).DateTime))
            .Where(g => g.Key >= from && g.Key <= to)
            .OrderBy(g => g.Key)
            .Select(g => new ScheduleDayCountDto(g.Key, g.Count()))
            .ToList();
    }

    public async Task<PagedScheduleAppointmentsDto> GetListAsync(ICurrentUser actor, ScheduleQuery query, int page, int pageSize, CancellationToken ct = default)
    {
        var organization = await RequireStaffAsync(actor, ct);
        RequireSaneRange(query.From, query.To, maxDays: 400);

        var filtered = Filtered(organization.Id, query);
        var total = await filtered.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 100);
        var current = Math.Max(page, 1);

        // Page on a narrow (Id, StartsAt) query first, then load just those
        // rows: sorting whole Appointment rows makes SQL Server budget for
        // the nvarchar(max) columns and request a memory grant a small
        // instance may not have (see SortByStart).
        var pageIds = await filtered.OrderBy(a => a.StartsAt).ThenBy(a => a.Id)
            .Select(a => a.Id).Skip((current - 1) * size).Take(size).ToListAsync(ct);
        var rows = SortByStart(await _db.Appointments.Where(a => pageIds.Contains(a.Id)).ToListAsync(ct));
        return new PagedScheduleAppointmentsDto(await ToDtosAsync(rows, ct), total, current, size);
    }

    public async Task<IReadOnlyList<SchedulePatientDto>> SearchPatientsAsync(ICurrentUser actor, string term, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Scheduling);
        term = term?.Trim() ?? string.Empty;
        if (term.Length < 2) return [];

        // Booking needs the whole roster, not a clinician's own caseload --
        // clinical: false. PatientsFor still scopes it to this organization
        // and excludes archived charts.
        var patients = _tenantAccess.PatientsFor(actor, clinical: false);
        var digits = new string(term.Where(char.IsDigit).ToArray());
        var dob = TryParseDate(term);

        patients = patients.Where(p =>
            p.FirstName.Contains(term) || p.LastName.Contains(term) ||
            (p.FirstName + " " + p.LastName).Contains(term) ||
            p.MedicalRecordNumber.Contains(term) ||
            (dob != null && p.DateOfBirth == dob) ||
            (digits.Length >= 3 && p.Phone != null &&
             p.Phone.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace(".", "").Contains(digits)));

        var rows = await patients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(20).ToListAsync(ct);
        return rows.Select(p => new SchedulePatientDto(p.Id, p.FullName, p.MedicalRecordNumber, p.DateOfBirth, p.Phone, p.Email, p.PrimaryLocationId)).ToList();
    }

    // ---- helpers ----

    /// <summary>Sort in memory, not in SQL. A day or week is a few hundred
    /// rows at most, but an ORDER BY over full Appointment rows (three
    /// nvarchar(max) columns) makes SQL Server request a large sort memory
    /// grant; on a memory-constrained SQL Express instance that request
    /// queues on RESOURCE_SEMAPHORE for ~25s before running.</summary>
    private static List<Appointment> SortByStart(List<Appointment> appointments) =>
        appointments.OrderBy(a => a.StartsAt).ThenBy(a => a.Id).ToList();

    private async Task<Organization> RequireStaffAsync(ICurrentUser actor, CancellationToken ct)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        return await _tenantAccess.OrganizationRequiredAsync(actor, ct);
    }

    /// <summary>Same visibility rule as IAppointmentService.ListForRangeAsync:
    /// every appointment in the organization (tenant via Patient). PTs and
    /// PTAs see the whole schedule, like admins (clinic decision, 2026-09-28).</summary>
    private IQueryable<Appointment> Filtered(Guid organizationId, ScheduleQuery query)
    {
        var patients = _db.Patients.Where(p => p.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(query.PatientSearch))
        {
            var term = query.PatientSearch.Trim();
            patients = patients.Where(p =>
                p.FirstName.Contains(term) || p.LastName.Contains(term) ||
                (p.FirstName + " " + p.LastName).Contains(term) || p.MedicalRecordNumber.Contains(term));
        }

        var appointments = _db.Appointments
            .Where(a => a.StartsAt < query.To && a.EndsAt > query.From)
            .Join(patients, a => a.PatientId, p => p.Id, (a, p) => a);

        if (query.ProviderId is Guid pid) appointments = appointments.Where(a => a.ProviderId == pid);
        if (query.LocationId is Guid lid) appointments = appointments.Where(a => a.LocationDetailId == lid);
        if (query.Status is AppointmentStatus status) appointments = appointments.Where(a => a.Status == status);
        if (query.AppointmentTypeId is Guid tid) appointments = appointments.Where(a => a.AppointmentTypeId == tid);
        return appointments;
    }

    private IQueryable<Provider> OrganizationProviders(Guid organizationId) =>
        _db.Providers.Where(p => p.OrganizationId == organizationId);

    private async Task<IReadOnlyList<ScheduleAppointmentDto>> ToDtosAsync(IReadOnlyList<Appointment> appointments, CancellationToken ct)
    {
        if (appointments.Count == 0) return [];

        var patientIds = appointments.Select(a => a.PatientId).Distinct().ToList();
        var providerIds = appointments.Select(a => a.ProviderId).OfType<Guid>().Distinct().ToList();
        var locationIds = appointments.Select(a => a.LocationDetailId).OfType<Guid>().Distinct().ToList();
        var typeIds = appointments.Select(a => a.AppointmentTypeId).OfType<Guid>().Distinct().ToList();

        var patients = await _db.Patients.Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var providers = await _db.Providers.Where(p => providerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var locations = await _db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var types = await _db.AppointmentTypes.Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

        return appointments.Select(a =>
        {
            var patient = patients[a.PatientId];
            var provider = a.ProviderId is Guid pid && providers.TryGetValue(pid, out var p) ? p : null;
            var location = a.LocationDetailId is Guid lid && locations.TryGetValue(lid, out var l) ? l : null;
            var type = a.AppointmentTypeId is Guid tid && types.TryGetValue(tid, out var t) ? t : null;
            return new ScheduleAppointmentDto(
                a.Id, a.ConfirmationNumber,
                patient.Id, patient.FullName, patient.MedicalRecordNumber, patient.DateOfBirth, patient.Phone,
                a.TherapistId, a.ProviderId, provider?.FullName, provider?.Credentials, provider?.Discipline,
                a.LocationDetailId, location?.Name, a.AppointmentTypeId, type?.Name, type?.Color,
                a.Kind, a.Status, a.StartsAt, a.EndsAt, (int)(a.EndsAt - a.StartsAt).TotalMinutes,
                a.IsHomeVisit, a.ReasonForVisit, a.SeriesId);
        }).ToList();
    }

    private static ProviderDaySummaryDto Summarize(
        IReadOnlyList<Appointment> appointments, IReadOnlyList<ProviderAvailability> windows, IReadOnlyList<ScheduleBlockDto> blocks,
        DateOnly date, TimeZoneInfo tz, DateTimeOffset now)
    {
        bool Counts(Appointment a) => a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);
        var active = appointments.Where(Counts).OrderBy(a => a.StartsAt).ToList();

        var windowRanges = windows.Select(w => (Start: Local(date, w.StartTime, tz), End: Local(date, w.EndTime, tz))).ToList();
        var workingMinutes = (int)windowRanges.Sum(w => (w.End - w.Start).TotalMinutes);
        var blockedMinutes = (int)windowRanges.Sum(w => blocks.Sum(b => OverlapMinutes(w.Start, w.End, b.StartsAt, b.EndsAt)));
        var bookedMinutes = (int)active.Sum(a => (a.EndsAt - a.StartsAt).TotalMinutes);
        var bookableMinutes = Math.Max(0, workingMinutes - blockedMinutes);

        return new ProviderDaySummaryDto(
            Appointments: appointments.Count,
            Scheduled: appointments.Count(a => a.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed),
            CheckedIn: appointments.Count(a => a.Status == AppointmentStatus.CheckedIn),
            InProgress: appointments.Count(a => a.Status == AppointmentStatus.InProgress),
            Completed: appointments.Count(a => a.Status == AppointmentStatus.Completed),
            Cancelled: appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            NoShow: appointments.Count(a => a.Status == AppointmentStatus.NoShow),
            Remaining: appointments.Count(a => a.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed),
            WorkingMinutes: workingMinutes,
            BookedMinutes: bookedMinutes,
            BlockedMinutes: blockedMinutes,
            AvailableMinutes: Math.Max(0, bookableMinutes - bookedMinutes),
            UtilizationPercent: bookableMinutes > 0 ? (int)Math.Round(bookedMinutes * 100.0 / bookableMinutes) : null,
            FirstAppointmentAt: active.FirstOrDefault()?.StartsAt,
            LastAppointmentEndsAt: active.Count > 0 ? active.Max(a => a.EndsAt) : null,
            CurrentAppointmentId: active.FirstOrDefault(a => a.StartsAt <= now && now < a.EndsAt)?.Id,
            NextAppointmentId: active.FirstOrDefault(a => a.StartsAt > now && a.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn)?.Id);
    }

    private static double OverlapMinutes(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd)
    {
        var start = aStart > bStart ? aStart : bStart;
        var end = aEnd < bEnd ? aEnd : bEnd;
        return end > start ? (end - start).TotalMinutes : 0;
    }

    private static DateTimeOffset Local(DateOnly date, TimeOnly time, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, tz.GetUtcOffset(local));
    }

    private static (DateTimeOffset Start, DateTimeOffset End) DayBounds(DateOnly date, TimeZoneInfo tz) =>
        (Local(date, TimeOnly.MinValue, tz), Local(date.AddDays(1), TimeOnly.MinValue, tz));

    private static ScheduleProviderDto ToProviderDto(Provider p) => new(
        p.Id, p.FullName, p.Credentials, p.Discipline, p.IsActive, p.UserId is not null, p.Locations.Select(l => l.Id).ToList(), p.UserId);

    private async Task<BookingConfiguration> ConfigAsync(Guid organizationId, CancellationToken ct) =>
        await _db.BookingConfigurations.FirstOrDefaultAsync(c => c.OrganizationId == organizationId, ct)
        ?? new BookingConfiguration { OrganizationId = organizationId };

    private static int SlotMinutes(BookingConfiguration config) =>
        AllowedSlotMinutes.Contains(config.StaffSlotMinutes) ? config.StaffSlotMinutes : 30;

    private async Task<TimeZoneInfo> TimeZoneAsync(Organization organization, Guid? locationId, CancellationToken ct)
    {
        string? id = organization.Timezone;
        if (locationId is Guid lid)
        {
            var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == lid && l.OrganizationId == organization.Id, ct)
                ?? throw new NotFoundException("Location was not found.");
            id = location.Timezone;
        }
        return TimeZoneInfo.TryFindSystemTimeZoneById(id ?? "UTC", out var tz) ? tz : TimeZoneInfo.Utc;
    }

    private static void RequireSaneRange(DateTimeOffset from, DateTimeOffset to, int maxDays)
    {
        if (to <= from || (to - from).TotalDays > maxDays)
        {
            throw new InvalidOperationException($"The date range must be positive and at most {maxDays} days.");
        }
    }

    private static DateOnly? TryParseDate(string term) =>
        DateOnly.TryParseExact(term, ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "M-d-yyyy", "MM-dd-yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

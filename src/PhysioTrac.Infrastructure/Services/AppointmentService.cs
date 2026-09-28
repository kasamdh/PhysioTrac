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

/// <summary>Staff-facing counterpart of `care/booking.py`'s transactional
/// create path — same two-layer double-booking defense (provider row lock +
/// a conflict re-check), scoped to an authenticated caller instead of the
/// public/portal surfaces.
///
/// Appointment has no OrganizationId of its own (scoped via
/// PatientId -> Patient.OrganizationId, the same pattern ProviderLicense
/// uses), so it isn't covered by EntityChangeAuditInterceptor's automatic
/// entity-change audit -- every mutating method here writes its own
/// explicit audit event for that reason.</summary>
public class AppointmentService : IAppointmentService
{
    private readonly PhysioTracDbContext _db;
    private readonly Application.Tenancy.ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;
    private readonly IReminderService _reminders;
    private readonly SchedulingRulesValidator _rules;

    public AppointmentService(
        PhysioTracDbContext db, Application.Tenancy.ITenantAccessService tenantAccess, IAuditService audit, IReminderService reminders)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
        _reminders = reminders;
        _rules = new SchedulingRulesValidator(db);
    }

    public async Task<Appointment> CreateAsync(CreateAppointmentRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (request.EndsAt <= request.StartsAt)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");
        await RequireOwnOrgReferencesAsync(organization.Id, request.TherapistId, request.ProviderId,
            request.LocationDetailId, request.RoomId, request.AppointmentTypeId, ct);
        await RequireTherapistMatchesProviderAsync(request.TherapistId, request.ProviderId, ct);

        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (request.ProviderId is Guid lockProviderId)
            {
                // Lock the provider row for the rest of this transaction so a
                // second, concurrent create for the same provider blocks here
                // until this one commits or rolls back — the primary defense
                // against the double-booking race (matches booking.py).
                if (_db.Database.IsRealSqlServer())
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT Id FROM Providers WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", new object[] { lockProviderId }, ct);
                }
                _ = await _db.Providers.FirstOrDefaultAsync(p => p.Id == lockProviderId && p.OrganizationId == organization.Id, ct)
                    ?? throw new NotFoundException("Provider was not found.");
            }

            var overridden = await EnforceRulesAsync(new SlotCheck(
                organization.Id, patient.Id, request.TherapistId, request.ProviderId, request.LocationDetailId, request.RoomId,
                request.AppointmentTypeId, request.Kind, request.StartsAt, request.EndsAt, ExcludeAppointmentId: null),
                request.OverrideReason, actor, organization.Id, ct);

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                TherapistId = request.TherapistId,
                ProviderId = request.ProviderId,
                LocationDetailId = request.LocationDetailId,
                RoomId = request.RoomId,
                AppointmentTypeId = request.AppointmentTypeId,
                Kind = request.Kind,
                Status = AppointmentStatus.Scheduled,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                IsHomeVisit = request.IsHomeVisit,
                ReasonForVisit = Truncate(request.ReasonForVisit, 240),
                PrivateNotes = Truncate(request.Notes, 2000),
                BookingSource = BookingSource.FrontDesk,
                CreatedById = actor.UserId,
            };
            _db.Appointments.Add(appointment);
            RecordStatusHistory(appointment.Id, null, AppointmentStatus.Scheduled, actor.UserId, null);
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAuditEventAsync(actor.UserId, "appointment.created", nameof(Appointment), appointment.Id, organization.Id,
                patientId: patient.Id, metadata: new { bookingSource = "front_desk" }, ct: ct);
            await AuditOverrideAsync(overridden, request.OverrideReason, appointment, actor, organization.Id, ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            await _reminders.ScheduleReminderAsync(appointment.Id, appointment.StartsAt, ct);
            return appointment;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    public async Task<Appointment> CancelAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);

        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn))
        {
            throw new InvalidOperationException("Only a scheduled, confirmed, or checked-in appointment can be cancelled.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.Cancelled, actor, organization.Id, "appointment.cancelled", null, ct);
        await _reminders.CancelReminderAsync(appointment.Id, ct);
        return appointment;
    }

    public async Task<Appointment> ConfirmAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new InvalidOperationException("Only a scheduled appointment can be confirmed.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.Confirmed, actor, organization.Id, "appointment.confirmed", null, ct);
        return appointment;
    }

    public async Task<Appointment> CheckInAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            throw new InvalidOperationException("Only a scheduled or confirmed appointment can be checked in.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.CheckedIn, actor, organization.Id, "appointment.checked_in", null, ct);
        return appointment;
    }

    public async Task<Appointment> StartVisitAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status != AppointmentStatus.CheckedIn)
        {
            throw new InvalidOperationException("Only a checked-in appointment can be started.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.InProgress, actor, organization.Id, "appointment.started", null, ct);
        return appointment;
    }

    public async Task<Appointment> CompleteAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status is not (AppointmentStatus.CheckedIn or AppointmentStatus.InProgress))
        {
            throw new InvalidOperationException("Only a checked-in or in-progress appointment can be marked completed.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.Completed, actor, organization.Id, "appointment.completed", null, ct);
        return appointment;
    }

    public async Task<Appointment> MarkNoShowAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            throw new InvalidOperationException("Only a scheduled or confirmed appointment (one the patient never checked in for) can be marked no-show.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.NoShow, actor, organization.Id, "appointment.no_show", null, ct);
        await _reminders.CancelReminderAsync(appointment.Id, ct);
        return appointment;
    }

    public async Task<Appointment> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var plan = await PlanRescheduleAsync(appointmentId, request, actor, ct);
        var (organization, appointment) = (plan.Organization, plan.Appointment);

        var overridden = await EnforceRulesAsync(plan.Slot, request.OverrideReason, actor, organization.Id, ct);

        var previousStart = appointment.StartsAt;
        var previousEnd = appointment.EndsAt;
        var previousProviderId = appointment.ProviderId;
        var previousTherapistId = appointment.TherapistId;
        var previousLocationId = appointment.LocationDetailId;
        appointment.StartsAt = plan.Slot.StartsAt;
        appointment.EndsAt = plan.Slot.EndsAt;
        appointment.ProviderId = plan.Slot.ProviderId;
        appointment.TherapistId = plan.Slot.TherapistId;
        appointment.LocationDetailId = plan.Slot.LocationId;
        appointment.RoomId = plan.Slot.RoomId;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // appointment.rescheduled on every move (existing consumers key off
        // it); appointment.provider_changed additionally when the clinician
        // changed, so "who moved this patient off my schedule" is one query.
        var moveMetadata = new
        {
            previousStart,
            newStart = appointment.StartsAt,
            previousEnd,
            newEnd = appointment.EndsAt,
            previousProviderId,
            newProviderId = appointment.ProviderId,
            previousLocationId,
            newLocationId = appointment.LocationDetailId,
            reason = string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim(),
        };
        await _audit.RecordAuditEventAsync(actor.UserId, "appointment.rescheduled", nameof(Appointment), appointment.Id, organization.Id,
            patientId: appointment.PatientId, metadata: moveMetadata, ct: ct);
        if (plan.ProviderChanged)
        {
            await _audit.RecordAuditEventAsync(actor.UserId, "appointment.provider_changed", nameof(Appointment), appointment.Id, organization.Id,
                patientId: appointment.PatientId,
                metadata: new { previousProviderId, newProviderId = appointment.ProviderId, previousTherapistId, newTherapistId = appointment.TherapistId }, ct: ct);
        }
        await AuditOverrideAsync(overridden, request.OverrideReason, appointment, actor, organization.Id, ct);

        await _reminders.ScheduleReminderAsync(appointment.Id, appointment.StartsAt, ct);
        return appointment;
    }

    public async Task<MoveCheckDto> ValidateRescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var plan = await PlanRescheduleAsync(appointmentId, request, actor, ct);
        var violations = await _rules.CheckAsync(plan.Slot, ct);
        var canOverride = violations.Count > 0 && await CanOverrideAsync(violations, actor, plan.Organization.Id, ct);

        var appointment = plan.Appointment;
        var patient = await _db.Patients.FirstAsync(p => p.Id == appointment.PatientId, ct);
        var providerIds = new[] { appointment.ProviderId, plan.Slot.ProviderId }.OfType<Guid>().ToList();
        var locationIds = new[] { appointment.LocationDetailId, plan.Slot.LocationId }.OfType<Guid>().ToList();
        var providerNames = await _db.Providers.Where(p => providerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.FirstName + " " + p.LastName).Trim(), ct);
        var locationNames = await _db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        string? Name(Dictionary<Guid, string> names, Guid? id) => id is Guid g && names.TryGetValue(g, out var n) ? n : null;

        return new MoveCheckDto(
            appointment.Id, patient.FullName,
            new AppointmentSlotSummaryDto(appointment.ProviderId, Name(providerNames, appointment.ProviderId),
                appointment.LocationDetailId, Name(locationNames, appointment.LocationDetailId), appointment.StartsAt, appointment.EndsAt),
            new AppointmentSlotSummaryDto(plan.Slot.ProviderId, Name(providerNames, plan.Slot.ProviderId),
                plan.Slot.LocationId, Name(locationNames, plan.Slot.LocationId), plan.Slot.StartsAt, plan.Slot.EndsAt),
            IsValid: violations.Count == 0, CanOverride: canOverride, Violations: violations);
    }

    private sealed record ReschedulePlan(
        Domain.Entities.Organization Organization, Appointment Appointment, SlotCheck Slot, bool ProviderChanged);

    /// <summary>Everything RescheduleAsync and its dry run share: permission,
    /// tenant ownership, state, and where the appointment would end up.</summary>
    private async Task<ReschedulePlan> PlanRescheduleAsync(
        Guid appointmentId, RescheduleAppointmentRequest request, ICurrentUser actor, CancellationToken ct)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);

        if (AccessControl.Enabled && actor.Role is UserRole.Therapist or UserRole.Assistant)
        {
            var config = await _db.BookingConfigurations.FirstOrDefaultAsync(c => c.OrganizationId == organization.Id, ct);
            if (config is { TherapistsMayReschedule: false })
            {
                throw new ForbiddenException("Your organization only allows front-desk and admin staff to reschedule appointments.");
            }
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow or AppointmentStatus.InProgress)
        {
            throw new InvalidOperationException("This appointment can no longer be rescheduled.");
        }
        if (request.EndsAt <= request.StartsAt)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        // Only the values this request actually changes need validating --
        // the appointment's existing ones were validated when it was booked.
        await RequireOwnOrgReferencesAsync(organization.Id, therapistId: null, request.ProviderId,
            request.LocationDetailId, request.RoomId, appointmentTypeId: null, ct);

        // Moving to a different provider moves the clinician too: TherapistId
        // is what the therapist conflict check and a Therapist/Assistant's
        // own-schedule view key off, so leaving it on the old clinician would
        // check the wrong calendar and show the visit on the wrong schedule.
        var therapistId = appointment.TherapistId;
        var providerChanged = request.ProviderId is Guid newProviderId && newProviderId != appointment.ProviderId;
        if (providerChanged)
        {
            var newProvider = await _db.Providers.FirstAsync(p => p.Id == request.ProviderId, ct);
            therapistId = newProvider.UserId
                ?? throw new InvalidOperationException($"{newProvider.FullName} has no login account, so appointments can't be assigned to them.");
        }

        var slot = new SlotCheck(
            organization.Id, appointment.PatientId, therapistId,
            request.ProviderId ?? appointment.ProviderId,
            request.LocationDetailId ?? appointment.LocationDetailId,
            request.RoomId ?? appointment.RoomId,
            appointment.AppointmentTypeId, appointment.Kind,
            request.StartsAt, request.EndsAt, ExcludeAppointmentId: appointment.Id);
        return new ReschedulePlan(organization, appointment, slot, providerChanged);
    }

    public async Task<IReadOnlyList<AppointmentStatusHistory>> GetStatusHistoryAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        var (_, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        return await _db.AppointmentStatusHistories
            .Where(h => h.AppointmentId == appointment.Id)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<AppointmentSeries> CreateSeriesAsync(CreateAppointmentSeriesRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var (organization, patient, checkedOccurrences) = await PlanSeriesAsync(request, actor, ct);

        var conflicting = checkedOccurrences.Where(o => o.Violations.Count > 0).ToList();
        var toBook = checkedOccurrences.Where(o => o.Violations.Count == 0).ToList();
        if (conflicting.Count > 0 && !request.SkipConflicting)
        {
            // All-or-nothing by default, so a series never half-books by accident.
            var formatted = string.Join(", ", conflicting.Select(o => $"{o.StartsAt:yyyy-MM-dd HH:mm} ({o.Violations[0].Message})"));
            throw new InvalidOperationException($"These occurrences can't be booked: {formatted}");
        }
        if (toBook.Count == 0)
        {
            throw new InvalidOperationException("None of these dates can be booked.");
        }

        var series = new AppointmentSeries
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            TherapistId = request.TherapistId,
            ProviderId = request.ProviderId,
            LocationDetailId = request.LocationDetailId,
            RoomId = request.RoomId,
            AppointmentTypeId = request.AppointmentTypeId,
            Kind = request.Kind,
            IntervalWeeks = request.IntervalWeeks,
            OccurrenceCount = toBook.Count,
            CreatedById = actor.UserId,
        };
        _db.AppointmentSeries.Add(series);
        await _db.SaveChangesAsync(ct);

        var created = new List<Appointment>();
        foreach (var occurrenceSlot in toBook)
        {
            var occurrence = new Appointment
            {
                PatientId = patient.Id,
                TherapistId = request.TherapistId,
                ProviderId = request.ProviderId,
                LocationDetailId = request.LocationDetailId,
                RoomId = request.RoomId,
                AppointmentTypeId = request.AppointmentTypeId,
                Kind = request.Kind,
                Status = AppointmentStatus.Scheduled,
                StartsAt = occurrenceSlot.StartsAt,
                EndsAt = occurrenceSlot.EndsAt,
                ReasonForVisit = Truncate(request.ReasonForVisit, 240),
                PrivateNotes = Truncate(request.Notes, 2000),
                BookingSource = BookingSource.FrontDesk,
                CreatedById = actor.UserId,
                SeriesId = series.Id,
            };
            _db.Appointments.Add(occurrence);
            created.Add(occurrence);
            RecordStatusHistory(occurrence.Id, null, AppointmentStatus.Scheduled, actor.UserId, "Created as part of a recurring series.");
        }
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "appointment_series.created", nameof(AppointmentSeries), series.Id, organization.Id,
            patientId: patient.Id,
            metadata: new
            {
                occurrenceCount = toBook.Count,
                skippedConflicts = conflicting.Select(o => o.StartsAt).ToList(),
                intervalWeeks = request.IntervalWeeks,
                daysOfWeek = request.DaysOfWeek?.Select(d => d.ToString()).ToList(),
            }, ct: ct);

        foreach (var occurrence in created)
        {
            await _reminders.ScheduleReminderAsync(occurrence.Id, occurrence.StartsAt, ct);
        }
        return series;
    }

    public async Task<SeriesPreviewDto> PreviewSeriesAsync(CreateAppointmentSeriesRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var (_, _, occurrences) = await PlanSeriesAsync(request, actor, ct);
        var bookable = occurrences.Count(o => o.Violations.Count == 0);
        return new SeriesPreviewDto(bookable, occurrences.Count - bookable, occurrences);
    }

    private const int MaxSeriesOccurrences = 52;

    /// <summary>Everything CreateSeriesAsync and its preview share:
    /// permission, tenant ownership, pattern validation, generating each
    /// occurrence, and running the scheduling rules against every one.</summary>
    private async Task<(Domain.Entities.Organization Organization, Patient Patient, IReadOnlyList<SeriesOccurrencePreviewDto> Occurrences)> PlanSeriesAsync(
        CreateAppointmentSeriesRequest request, ICurrentUser actor, CancellationToken ct)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (request.FirstEndsAt <= request.FirstStartsAt)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }
        if (request.IntervalWeeks < 1 || (request.EndDate is null && (request.OccurrenceCount < 1 || request.OccurrenceCount > MaxSeriesOccurrences)))
        {
            throw new InvalidOperationException($"Interval must be at least 1 week and occurrence count must be between 1 and {MaxSeriesOccurrences}.");
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");
        await RequireOwnOrgReferencesAsync(organization.Id, request.TherapistId, request.ProviderId,
            request.LocationDetailId, request.RoomId, request.AppointmentTypeId, ct);
        await RequireTherapistMatchesProviderAsync(request.TherapistId, request.ProviderId, ct);

        var location = request.LocationDetailId is Guid lid ? await _db.Locations.FirstOrDefaultAsync(l => l.Id == lid, ct) : null;
        var tz = await _rules.TimeZoneForAsync(organization.Id, location, ct);
        var occurrences = GenerateOccurrences(request, tz);

        var checkedOccurrences = new List<SeriesOccurrencePreviewDto>();
        foreach (var (startsAt, endsAt) in occurrences)
        {
            var violations = await _rules.CheckAsync(new SlotCheck(
                organization.Id, patient.Id, request.TherapistId, request.ProviderId, request.LocationDetailId, request.RoomId,
                request.AppointmentTypeId, request.Kind, startsAt, endsAt, ExcludeAppointmentId: null), ct);
            checkedOccurrences.Add(new SeriesOccurrencePreviewDto(startsAt, endsAt, violations));
        }
        return (organization, patient, checkedOccurrences);
    }

    /// <summary>The pattern's dates, each at the first visit's clinic-local
    /// time of day. Built from local wall-clock time, not by adding 7-day
    /// spans to an offset, so a 9:00 visit stays at 9:00 across a DST change.
    /// Weeks are counted from the Monday of the first visit's week, so
    /// "every 2 weeks, Mon + Thu" keeps both days in the same weeks.</summary>
    private static List<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> GenerateOccurrences(CreateAppointmentSeriesRequest request, TimeZoneInfo tz)
    {
        var firstLocal = TimeZoneInfo.ConvertTime(request.FirstStartsAt, tz);
        var firstDate = DateOnly.FromDateTime(firstLocal.DateTime);
        var timeOfDay = TimeOnly.FromDateTime(firstLocal.DateTime);
        var duration = request.FirstEndsAt - request.FirstStartsAt;
        var days = request.DaysOfWeek is { Count: > 0 } ? request.DaysOfWeek.ToHashSet() : [firstDate.ToWeekday()];
        var weekStart = firstDate.AddDays(-(int)firstDate.ToWeekday());

        if (request.EndDate is DateOnly end && end < firstDate)
        {
            throw new InvalidOperationException("The end date must be on or after the first visit.");
        }

        var result = new List<(DateTimeOffset, DateTimeOffset)>();
        for (var date = firstDate; date <= firstDate.AddYears(2); date = date.AddDays(1))
        {
            if (request.EndDate is DateOnly until ? date > until : result.Count >= request.OccurrenceCount) break;
            var weekIndex = (date.DayNumber - weekStart.DayNumber) / 7;
            if (weekIndex % request.IntervalWeeks != 0 || !days.Contains(date.ToWeekday())) continue;

            if (result.Count == MaxSeriesOccurrences)
            {
                throw new InvalidOperationException($"A series can have at most {MaxSeriesOccurrences} visits -- choose an earlier end date.");
            }
            var local = date.ToDateTime(timeOfDay);
            var startsAt = new DateTimeOffset(local, tz.GetUtcOffset(local));
            result.Add((startsAt, startsAt + duration));
        }
        if (result.Count == 0)
        {
            throw new InvalidOperationException("That pattern doesn't produce any visits.");
        }
        return result;
    }

    public async Task<AppointmentSeries> GetSeriesAsync(Guid seriesId, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var series = await _db.AppointmentSeries.Include(s => s.Occurrences)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException("Appointment series was not found.");
        if (series.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Appointment series was not found.");
        }
        return series;
    }

    public async Task<int> CancelSeriesAsync(Guid seriesId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var series = await GetSeriesAsync(seriesId, actor, ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var now = DateTimeOffset.UtcNow;
        var toCancel = series.Occurrences
            .Where(a => a.StartsAt >= now && a.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed)
            .ToList();

        foreach (var occurrence in toCancel)
        {
            RecordStatusHistory(occurrence.Id, occurrence.Status, AppointmentStatus.Cancelled, actor.UserId, "Cancelled as part of the series.");
            occurrence.Status = AppointmentStatus.Cancelled;
            occurrence.UpdatedAt = now;
        }
        series.IsActive = false;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "appointment_series.cancelled", nameof(AppointmentSeries), series.Id, organization.Id,
            patientId: series.PatientId, metadata: new { occurrencesCancelled = toCancel.Count }, ct: ct);

        return toCancel.Count;
    }

    public async Task<IReadOnlyList<Appointment>> ListForRangeAsync(
        ICurrentUser actor, DateTimeOffset from, DateTimeOffset to,
        Guid? providerId = null, Guid? locationDetailId = null, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        // A portal (Patient-role) account only ever sees its own chart's
        // appointments -- the same unconditional narrowing PatientsFor applies.
        var patients = _db.Patients.Where(p => p.OrganizationId == organization.Id);
        if (actor.Role == UserRole.Patient)
        {
            patients = patients.Where(p => p.PortalUserId == actor.UserId);
        }

        var query = _db.Appointments
            .Where(a => a.StartsAt < to && a.EndsAt > from)
            .Join(patients, a => a.PatientId, p => p.Id, (a, p) => a);

        // Staff -- PTs and PTAs included -- see the organization's whole
        // schedule (clinic decision, 2026-09-28); only Patient is narrowed.
        if (providerId is Guid pid)
        {
            query = query.Where(a => a.ProviderId == pid);
        }
        if (locationDetailId is Guid lid)
        {
            query = query.Where(a => a.LocationDetailId == lid);
        }

        // Sorted in memory rather than by SQL -- see ScheduleService.SortByStart.
        return (await query.ToListAsync(ct)).OrderBy(a => a.StartsAt).ToList();
    }

    /// <summary>Runs every scheduling rule and throws a
    /// SchedulingConflictException unless the slot is clean -- or this caller
    /// may override every broken rule and supplied a reason. Returns the
    /// violations that were overridden (empty when none), for auditing once
    /// the appointment is saved.</summary>
    private async Task<IReadOnlyList<SchedulingViolation>> EnforceRulesAsync(
        SlotCheck slot, string? overrideReason, ICurrentUser actor, Guid organizationId, CancellationToken ct)
    {
        var violations = await _rules.CheckAsync(slot, ct);
        if (violations.Count == 0) return violations;

        var canOverride = await CanOverrideAsync(violations, actor, organizationId, ct);
        if (canOverride && !string.IsNullOrWhiteSpace(overrideReason))
        {
            return violations;
        }
        throw new SchedulingConflictException(violations, canOverride);
    }

    /// <summary>Only Admin/Director may override, only rules marked
    /// overridable, and double-booking only when the organization has opted
    /// in -- availability (hours, time off, closures) is overridable by those
    /// roles regardless.</summary>
    private async Task<bool> CanOverrideAsync(IReadOnlyList<SchedulingViolation> violations, ICurrentUser actor, Guid organizationId, CancellationToken ct)
    {
        if (!Application.Tenancy.RoleSets.ScheduleOverride.Contains(actor.Role)) return false;
        if (violations.Any(v => !v.Overridable)) return false;
        if (violations.Any(v => v.Code == SchedulingViolationCodes.DoubleBooked))
        {
            var config = await _db.BookingConfigurations.FirstOrDefaultAsync(c => c.OrganizationId == organizationId, ct);
            return config?.AllowDoubleBookOverride == true;
        }
        return true;
    }

    private async Task AuditOverrideAsync(IReadOnlyList<SchedulingViolation> overridden, string? reason, Appointment appointment,
        ICurrentUser actor, Guid organizationId, CancellationToken ct)
    {
        if (overridden.Count == 0) return;
        await _audit.RecordAuditEventAsync(actor.UserId, "schedule.conflict_override", nameof(Appointment), appointment.Id, organizationId,
            patientId: appointment.PatientId,
            metadata: new
            {
                reason = reason!.Trim(),
                rules = overridden.Select(v => v.Code).ToList(),
                providerId = appointment.ProviderId,
                startsAt = appointment.StartsAt,
                endsAt = appointment.EndsAt,
            }, ct: ct);
    }

    private async Task TransitionStatusAsync(
        Appointment appointment, AppointmentStatus newStatus, ICurrentUser actor, Guid organizationId, string auditAction, string? reason, CancellationToken ct)
    {
        var previousStatus = appointment.Status;
        RecordStatusHistory(appointment.Id, previousStatus, newStatus, actor.UserId, reason);
        appointment.Status = newStatus;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, auditAction, nameof(Appointment), appointment.Id, organizationId,
            patientId: appointment.PatientId, metadata: new { from = previousStatus.ToString(), to = newStatus.ToString() }, ct: ct);
    }

    private void RecordStatusHistory(Guid appointmentId, AppointmentStatus? fromStatus, AppointmentStatus toStatus, Guid changedById, string? reason)
    {
        _db.AppointmentStatusHistories.Add(new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedById = changedById,
            Reason = reason,
        });
    }

    private async Task<(Domain.Entities.Organization Organization, Appointment Appointment)> LoadAppointmentInOrgAsync(
        Guid appointmentId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment was not found.");

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == appointment.PatientId, ct);
        if (patient is null || patient.OrganizationId != organization.Id
            || (actor.Role == UserRole.Patient && patient.PortalUserId != actor.UserId))
        {
            throw new NotFoundException("Appointment was not found.");
        }

        return (organization, appointment);
    }

    /// <summary>A booking's provider and therapist must be the same clinician
    /// when the provider has a login -- otherwise the visit would sit in one
    /// provider's Day-view column while conflict-checking and "my schedule"
    /// follow a different person.</summary>
    private async Task RequireTherapistMatchesProviderAsync(Guid therapistId, Guid? providerId, CancellationToken ct)
    {
        if (providerId is not Guid pid) return;
        var providerUserId = await _db.Providers.Where(p => p.Id == pid).Select(p => p.UserId).FirstOrDefaultAsync(ct);
        if (providerUserId is Guid uid && uid != therapistId)
        {
            throw new InvalidOperationException("The therapist must be the provider's own login.");
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    /// <summary>Every id a client sends must belong to the caller's own
    /// organization -- never trusted just because it was sent. A foreign
    /// therapist/provider id would otherwise let one tenant probe another's
    /// calendar through the conflict check's "already has an appointment"
    /// answer. Null means "not being set", so nothing to check.</summary>
    private async Task RequireOwnOrgReferencesAsync(
        Guid organizationId, Guid? therapistId, Guid? providerId, Guid? locationId, Guid? roomId, Guid? appointmentTypeId,
        CancellationToken ct)
    {
        if (therapistId is Guid tid && !await _db.Users.AnyAsync(u =>
                u.Id == tid && u.OrganizationId == organizationId && u.Status == UserStatus.Active &&
                (u.Role == UserRole.Therapist || u.Role == UserRole.Assistant), ct))
        {
            throw new NotFoundException("Therapist was not found.");
        }
        if (providerId is Guid pid && !await _db.Providers.AnyAsync(p => p.Id == pid && p.OrganizationId == organizationId, ct))
        {
            throw new NotFoundException("Provider was not found.");
        }
        if (locationId is Guid lid && !await _db.Locations.AnyAsync(l => l.Id == lid && l.OrganizationId == organizationId, ct))
        {
            throw new NotFoundException("Location was not found.");
        }
        if (roomId is Guid rid && !await _db.Rooms.AnyAsync(r => r.Id == rid && r.Location!.OrganizationId == organizationId, ct))
        {
            throw new NotFoundException("Room was not found.");
        }
        if (appointmentTypeId is Guid atid && !await _db.AppointmentTypes.AnyAsync(t => t.Id == atid && t.OrganizationId == organizationId, ct))
        {
            throw new NotFoundException("Appointment type was not found.");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
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

    public AppointmentService(
        PhysioTracDbContext db, Application.Tenancy.ITenantAccessService tenantAccess, IAuditService audit, IReminderService reminders)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
        _reminders = reminders;
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

            var conflict = await FindConflictAsync(
                request.PatientId, request.TherapistId, request.ProviderId, request.RoomId,
                request.StartsAt, request.EndsAt, excludeAppointmentId: null, ct);
            if (conflict is not null)
            {
                throw new InvalidOperationException(conflict);
            }

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
                ReasonForVisit = request.ReasonForVisit?.Length > 240 ? request.ReasonForVisit[..240] : request.ReasonForVisit,
                BookingSource = BookingSource.FrontDesk,
                CreatedById = actor.UserId,
            };
            _db.Appointments.Add(appointment);
            RecordStatusHistory(appointment.Id, null, AppointmentStatus.Scheduled, actor.UserId, null);
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAuditEventAsync(actor.UserId, "appointment.created", nameof(Appointment), appointment.Id, organization.Id,
                patientId: patient.Id, metadata: new { bookingSource = "front_desk" }, ct: ct);

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
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            throw new InvalidOperationException("Only a scheduled or confirmed appointment can be checked in.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.CheckedIn, actor, organization.Id, "appointment.checked_in", null, ct);
        return appointment;
    }

    public async Task<Appointment> CompleteAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);
        if (appointment.Status != AppointmentStatus.CheckedIn)
        {
            throw new InvalidOperationException("Only a checked-in appointment can be marked completed.");
        }

        await TransitionStatusAsync(appointment, AppointmentStatus.Completed, actor, organization.Id, "appointment.completed", null, ct);
        return appointment;
    }

    public async Task<Appointment> MarkNoShowAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
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
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var (organization, appointment) = await LoadAppointmentInOrgAsync(appointmentId, actor, ct);

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow)
        {
            throw new InvalidOperationException("This appointment can no longer be rescheduled.");
        }
        if (request.EndsAt <= request.StartsAt)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        var providerId = request.ProviderId ?? appointment.ProviderId;
        var roomId = request.RoomId ?? appointment.RoomId;
        var conflict = await FindConflictAsync(
            appointment.PatientId, appointment.TherapistId, providerId, roomId,
            request.StartsAt, request.EndsAt, excludeAppointmentId: appointment.Id, ct);
        if (conflict is not null)
        {
            throw new InvalidOperationException(conflict);
        }

        var previousStart = appointment.StartsAt;
        appointment.StartsAt = request.StartsAt;
        appointment.EndsAt = request.EndsAt;
        appointment.ProviderId = providerId;
        appointment.LocationDetailId = request.LocationDetailId ?? appointment.LocationDetailId;
        appointment.RoomId = roomId;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "appointment.rescheduled", nameof(Appointment), appointment.Id, organization.Id,
            patientId: appointment.PatientId,
            metadata: new { previousStart, newStart = appointment.StartsAt }, ct: ct);

        await _reminders.ScheduleReminderAsync(appointment.Id, appointment.StartsAt, ct);
        return appointment;
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
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Scheduling);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (request.FirstEndsAt <= request.FirstStartsAt)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }
        if (request.IntervalWeeks < 1 || request.OccurrenceCount < 1 || request.OccurrenceCount > 52)
        {
            throw new InvalidOperationException("Interval must be at least 1 week and occurrence count must be between 1 and 52.");
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");

        var duration = request.FirstEndsAt - request.FirstStartsAt;
        var occurrenceTimes = Enumerable.Range(0, request.OccurrenceCount)
            .Select(i => request.FirstStartsAt.AddDays(7 * request.IntervalWeeks * i))
            .ToList();

        // All-or-nothing: check every occurrence for a conflict before
        // creating any of them, so a series either books cleanly or the
        // caller gets back exactly which dates collided and can adjust.
        var conflictingDates = new List<DateTimeOffset>();
        foreach (var startsAt in occurrenceTimes)
        {
            var conflict = await FindConflictAsync(
                request.PatientId, request.TherapistId, request.ProviderId, request.RoomId,
                startsAt, startsAt + duration, excludeAppointmentId: null, ct);
            if (conflict is not null) conflictingDates.Add(startsAt);
        }
        if (conflictingDates.Count > 0)
        {
            var formatted = string.Join(", ", conflictingDates.Select(d => d.ToString("yyyy-MM-dd HH:mm")));
            throw new InvalidOperationException($"These occurrences conflict with an existing appointment: {formatted}.");
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
            OccurrenceCount = request.OccurrenceCount,
            CreatedById = actor.UserId,
        };
        _db.AppointmentSeries.Add(series);
        await _db.SaveChangesAsync(ct);

        foreach (var startsAt in occurrenceTimes)
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
                StartsAt = startsAt,
                EndsAt = startsAt + duration,
                ReasonForVisit = request.ReasonForVisit,
                BookingSource = BookingSource.FrontDesk,
                CreatedById = actor.UserId,
                SeriesId = series.Id,
            };
            _db.Appointments.Add(occurrence);
            RecordStatusHistory(occurrence.Id, null, AppointmentStatus.Scheduled, actor.UserId, "Created as part of a recurring series.");
        }
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "appointment_series.created", nameof(AppointmentSeries), series.Id, organization.Id,
            patientId: patient.Id, metadata: new { occurrenceCount = request.OccurrenceCount, intervalWeeks = request.IntervalWeeks }, ct: ct);

        return series;
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

        var query = _db.Appointments
            .Where(a => a.StartsAt < to && a.EndsAt > from)
            .Join(_db.Patients.Where(p => p.OrganizationId == organization.Id), a => a.PatientId, p => p.Id, (a, p) => a);

        if (actor.Role is UserRole.Therapist or UserRole.Assistant)
        {
            query = query.Where(a => a.TherapistId == actor.UserId);
        }
        if (providerId is Guid pid)
        {
            query = query.Where(a => a.ProviderId == pid);
        }
        if (locationDetailId is Guid lid)
        {
            query = query.Where(a => a.LocationDetailId == lid);
        }

        return await query.OrderBy(a => a.StartsAt).ToListAsync(ct);
    }

    /// <summary>Checks all three resources this phase asks for -- provider,
    /// patient, and room -- against every non-cancelled/non-no-show
    /// appointment overlapping the requested window. Therapist and
    /// (when set) Provider are checked separately since they're distinct
    /// fields that usually, but don't always, refer to the same person.</summary>
    private async Task<string?> FindConflictAsync(
        Guid patientId, Guid therapistId, Guid? providerId, Guid? roomId,
        DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? excludeAppointmentId, CancellationToken ct)
    {
        var excludeId = excludeAppointmentId ?? Guid.Empty;
        var overlapping = _db.Appointments.Where(a =>
            a.Id != excludeId &&
            a.StartsAt < endsAt && a.EndsAt > startsAt &&
            a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow);

        if (await overlapping.AnyAsync(a => a.TherapistId == therapistId, ct))
        {
            return "This therapist already has an appointment during this time.";
        }
        if (providerId is Guid pid && await overlapping.AnyAsync(a => a.ProviderId == pid, ct))
        {
            return "This provider already has an appointment during this time.";
        }
        if (await overlapping.AnyAsync(a => a.PatientId == patientId, ct))
        {
            return "This patient already has another appointment during this time.";
        }
        if (roomId is Guid rid && await overlapping.AnyAsync(a => a.RoomId == rid, ct))
        {
            return "This room is already booked during this time.";
        }
        return null;
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
        if (patient is null || patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        return (organization, appointment);
    }
}

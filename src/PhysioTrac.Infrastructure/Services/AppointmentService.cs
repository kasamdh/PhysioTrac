using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Staff-facing counterpart of `care/booking.py`'s transactional
/// create path — same two-layer double-booking defense (provider row lock +
/// a conflict re-check), scoped to an authenticated caller instead of the
/// public/portal surfaces.</summary>
public class AppointmentService : IAppointmentService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public AppointmentService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
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
            if (request.ProviderId is Guid providerId)
            {
                // Lock the provider row for the rest of this transaction so a
                // second, concurrent create for the same provider blocks here
                // until this one commits or rolls back — the primary defense
                // against the double-booking race (matches booking.py).
                if (_db.Database.IsRealSqlServer())
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT Id FROM Providers WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", new object[] { providerId }, ct);
                }
                var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId && p.OrganizationId == organization.Id, ct)
                    ?? throw new NotFoundException("Provider was not found.");
            }

            // Model-level backstop against double-booking a therapist,
            // re-checked inside the same transaction as the row lock above —
            // matches Appointment.clean()'s conflict query in the original.
            var hasConflict = await _db.Appointments.AnyAsync(a =>
                a.TherapistId == request.TherapistId &&
                a.StartsAt < request.EndsAt && a.EndsAt > request.StartsAt &&
                a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow, ct);
            if (hasConflict)
            {
                throw new InvalidOperationException("This therapist already has an appointment during this time.");
            }

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                TherapistId = request.TherapistId,
                ProviderId = request.ProviderId,
                LocationDetailId = request.LocationDetailId,
                AppointmentTypeId = request.AppointmentTypeId,
                Kind = request.Kind,
                Status = AppointmentStatus.Scheduled,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                IsHomeVisit = request.IsHomeVisit,
                ReasonForVisit = request.ReasonForVisit?.Length > 240 ? request.ReasonForVisit[..240] : request.ReasonForVisit,
                BookingSource = Domain.Enums.BookingSource.FrontDesk,
                CreatedById = actor.UserId,
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAuditEventAsync(actor.UserId, "appointment.created", nameof(Appointment), appointment.Id, organization.Id,
                patientId: patient.Id, metadata: new { bookingSource = "front_desk" }, ct: ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
            return appointment;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    public async Task<Appointment> CancelAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment was not found.");

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == appointment.PatientId, ct);
        if (patient is null || patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        if (appointment.Status != AppointmentStatus.Scheduled && appointment.Status != AppointmentStatus.CheckedIn)
        {
            throw new InvalidOperationException("Only a scheduled or checked-in appointment can be cancelled.");
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "appointment.cancelled", nameof(Appointment), appointment.Id, organization.Id,
            patientId: patient.Id, metadata: new { source = "staff" }, ct: ct);

        return appointment;
    }

    public async Task<IReadOnlyList<Appointment>> ListForRangeAsync(ICurrentUser actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var query = _db.Appointments
            .Where(a => a.StartsAt < to && a.EndsAt > from)
            .Join(_db.Patients.Where(p => p.OrganizationId == organization.Id), a => a.PatientId, p => p.Id, (a, p) => a);

        if (actor.Role is Domain.Enums.UserRole.Therapist or Domain.Enums.UserRole.Assistant)
        {
            query = query.Where(a => a.TherapistId == actor.UserId);
        }

        return await query.OrderBy(a => a.StartsAt).ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Booking;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of the patient-portal half of `care/booking.py`.</summary>
public class PortalBookingService : IPortalBookingService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAvailabilityService _availability;
    private readonly IAuditService _audit;

    public PortalBookingService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAvailabilityService availability, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _availability = availability;
        _audit = audit;
    }

    public async Task<Appointment> CreateAsync(ICurrentUser patientUser, PortalBookingRequest request, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        var config = await GetOrCreateConfigAsync(patient.OrganizationId, ct);
        if (!config.OnlineBookingEnabled)
        {
            throw new BookingNotFoundException("Online scheduling is not currently available for this organization.");
        }
        if (!config.AllowReturningPatients)
        {
            throw new BookingNotFoundException("Online scheduling is not currently available for existing patients.");
        }

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId && l.OrganizationId == patient.OrganizationId && l.IsActive, ct)
            ?? throw new BookingNotFoundException("This location is not available for booking.");
        var appointmentType = await _db.AppointmentTypes.FirstOrDefaultAsync(
                a => a.Id == request.AppointmentTypeId && a.OrganizationId == patient.OrganizationId && a.IsActive && a.OnlineBookingEnabled, ct)
            ?? throw new BookingNotFoundException("This appointment type is not available for booking.");
        if (appointmentType.RequiresNewPatient)
        {
            throw new BookingValidationException("This appointment type is only available for new patients.");
        }
        var provider = await _db.Providers.FirstOrDefaultAsync(
                p => p.Id == request.ProviderId && p.OrganizationId == patient.OrganizationId && p.IsActive && p.OnlineBookingEnabled && p.UserId != null, ct)
            ?? throw new BookingNotFoundException("This provider is not available for booking.");
        if (!await _db.Providers.Where(p => p.Id == provider.Id).AnyAsync(p => p.Locations.Any(l => l.Id == location.Id), ct))
        {
            throw new BookingNotFoundException("This provider does not work at the selected location.");
        }
        if (!await _db.ProviderAppointmentTypes.AnyAsync(l => l.ProviderId == provider.Id && l.AppointmentTypeId == appointmentType.Id && l.Active, ct))
        {
            throw new BookingNotFoundException("This provider does not offer the selected appointment type.");
        }

        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (_db.Database.IsRealSqlServer())
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT Id FROM Providers WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", new object[] { provider.Id }, ct);
            }

            var localDate = DateOnly.FromDateTime(request.StartDatetime.UtcDateTime);
            var slots = await _availability.GetProviderSlotsAsync(provider.Id, location.Id, appointmentType.Id, localDate, ct: ct);
            var matchingSlot = slots.FirstOrDefault(s => s.Start == request.StartDatetime)
                ?? throw new SlotNoLongerAvailableException();

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                TherapistId = provider.UserId!.Value,
                ProviderId = provider.Id,
                LocationDetailId = location.Id,
                AppointmentTypeId = appointmentType.Id,
                Kind = appointmentType.DefaultKind ?? AppointmentKind.FollowUp,
                Status = AppointmentStatus.Scheduled,
                StartsAt = matchingSlot.Start,
                EndsAt = matchingSlot.End,
                IsHomeVisit = false,
                ReasonForVisit = request.ReasonForVisit?.Length > 240 ? request.ReasonForVisit[..240] : request.ReasonForVisit,
                BookingSource = BookingSource.PatientPortal,
                CreatedById = patientUser.UserId,
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAuditEventAsync(patientUser.UserId, "appointment.created", nameof(Appointment), appointment.Id, patient.OrganizationId,
                patientId: patient.Id, metadata: new { bookingSource = "patient_portal" }, ct: ct);

            if (transaction is not null) await transaction.CommitAsync(ct);
            return appointment;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    public async Task<Appointment> CancelAsync(ICurrentUser patientUser, Guid appointmentId, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        var config = await GetOrCreateConfigAsync(patient.OrganizationId, ct);
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id, ct)
            ?? throw new BookingNotFoundException("Appointment not found.");
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new BookingValidationException("Only scheduled appointments can be cancelled.");
        }
        AssertWithinChangeWindow(appointment, config);

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(patientUser.UserId, "appointment.cancelled", nameof(Appointment), appointment.Id, patient.OrganizationId,
            patientId: patient.Id, metadata: new { source = "patient_portal" }, ct: ct);
        return appointment;
    }

    public async Task<Appointment> RescheduleAsync(ICurrentUser patientUser, Guid appointmentId, PortalRescheduleRequest request, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        var config = await GetOrCreateConfigAsync(patient.OrganizationId, ct);
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id, ct)
            ?? throw new BookingNotFoundException("Appointment not found.");
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new BookingValidationException("Only scheduled appointments can be rescheduled.");
        }
        AssertWithinChangeWindow(appointment, config);
        if (appointment.ProviderId is null || appointment.LocationDetailId is null || appointment.AppointmentTypeId is null)
        {
            throw new BookingValidationException("This appointment cannot be rescheduled online. Please call the clinic.");
        }

        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (_db.Database.IsRealSqlServer())
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT Id FROM Providers WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", new object[] { appointment.ProviderId }, ct);
            }

            var localDate = DateOnly.FromDateTime(request.StartDatetime.UtcDateTime);
            var slots = await _availability.GetProviderSlotsAsync(
                appointment.ProviderId.Value, appointment.LocationDetailId.Value, appointment.AppointmentTypeId.Value, localDate,
                excludeAppointmentId: appointment.Id, ct: ct);
            var matchingSlot = slots.FirstOrDefault(s => s.Start == request.StartDatetime)
                ?? throw new SlotNoLongerAvailableException();

            appointment.StartsAt = matchingSlot.Start;
            appointment.EndsAt = matchingSlot.End;
            appointment.ConfirmedAt = null;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAuditEventAsync(patientUser.UserId, "appointment.rescheduled", nameof(Appointment), appointment.Id, patient.OrganizationId,
                patientId: patient.Id, metadata: new { source = "patient_portal" }, ct: ct);

            if (transaction is not null) await transaction.CommitAsync(ct);
            return appointment;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    public async Task<Appointment> ConfirmAsync(ICurrentUser patientUser, Guid appointmentId, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id, ct)
            ?? throw new BookingNotFoundException("Appointment not found.");
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new BookingValidationException("Only scheduled appointments can be confirmed.");
        }
        if (appointment.ConfirmedAt is null)
        {
            appointment.ConfirmedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.RecordAuditEventAsync(patientUser.UserId, "appointment.confirmed", nameof(Appointment), appointment.Id, patient.OrganizationId,
                patientId: patient.Id, metadata: new { source = "patient_portal" }, ct: ct);
        }
        return appointment;
    }

    public async Task<Waitlist> JoinWaitlistAsync(ICurrentUser patientUser, JoinWaitlistRequest request, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);

        Location? location = null;
        if (request.LocationId is Guid locId)
        {
            location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locId && l.OrganizationId == patient.OrganizationId && l.IsActive, ct)
                ?? throw new BookingNotFoundException("This location is not available.");
        }
        AppointmentType? appointmentType = null;
        if (request.AppointmentTypeId is Guid typeId)
        {
            appointmentType = await _db.AppointmentTypes.FirstOrDefaultAsync(a => a.Id == typeId && a.OrganizationId == patient.OrganizationId && a.IsActive, ct)
                ?? throw new BookingNotFoundException("This appointment type is not available.");
        }
        Provider? provider = null;
        if (request.ProviderId is Guid provId)
        {
            provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == provId && p.OrganizationId == patient.OrganizationId && p.IsActive, ct)
                ?? throw new BookingNotFoundException("This provider is not available.");
        }

        // Idempotent join: re-submitting the same preferences while already
        // active just returns the existing entry rather than duplicating it.
        Guid? locationId = location?.Id;
        Guid? appointmentTypeId = appointmentType?.Id;
        Guid? providerId = provider?.Id;
        var existing = await _db.Waitlists.FirstOrDefaultAsync(w =>
            w.PatientId == patient.Id && w.Status == WaitlistStatus.Active &&
            w.LocationId == locationId && w.AppointmentTypeId == appointmentTypeId && w.ProviderId == providerId, ct);
        if (existing is not null) return existing;

        if (request.LatestDate is DateOnly latest && latest < request.EarliestDate)
        {
            throw new BookingValidationException("End of range cannot precede the start of the range.", "latestDate");
        }

        var entry = new Waitlist
        {
            OrganizationId = patient.OrganizationId,
            PatientId = patient.Id,
            LocationId = location?.Id,
            AppointmentTypeId = appointmentType?.Id,
            ProviderId = provider?.Id,
            EarliestDate = request.EarliestDate,
            LatestDate = request.LatestDate,
            Notes = request.Notes?.Length > 240 ? request.Notes[..240] : request.Notes,
        };
        _db.Waitlists.Add(entry);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(patientUser.UserId, "waitlist.joined", nameof(Waitlist), entry.Id, patient.OrganizationId, patientId: patient.Id, ct: ct);
        return entry;
    }

    public async Task<Waitlist> LeaveWaitlistAsync(ICurrentUser patientUser, Guid entryId, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        var entry = await _db.Waitlists.FirstOrDefaultAsync(w => w.Id == entryId && w.PatientId == patient.Id, ct)
            ?? throw new BookingNotFoundException("Waitlist entry not found.");
        if (entry.Status == WaitlistStatus.Active)
        {
            entry.Status = WaitlistStatus.Cancelled;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.RecordAuditEventAsync(patientUser.UserId, "waitlist.left", nameof(Waitlist), entry.Id, patient.OrganizationId, patientId: patient.Id, ct: ct);
        }
        return entry;
    }

    public async Task<IReadOnlyList<Waitlist>> ListWaitlistAsync(ICurrentUser patientUser, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        return await _db.Waitlists.Where(w => w.PatientId == patient.Id).OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
    }

    private void AssertWithinChangeWindow(Appointment appointment, BookingConfiguration config)
    {
        // Home-visit-specific cancellation windows (Mobile Care) aren't
        // ported yet — always uses the general BookingConfiguration cutoff,
        // a documented simplification vs. the original's home-visit branch.
        if (appointment.StartsAt - DateTimeOffset.UtcNow < TimeSpan.FromHours(config.PatientChangeCutoffHours))
        {
            throw new ChangeCutoffException();
        }
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
}

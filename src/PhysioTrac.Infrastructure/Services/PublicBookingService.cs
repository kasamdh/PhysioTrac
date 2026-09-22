using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Booking;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `care/api/public_booking.py` + the public half of
/// `care/booking.py`.</summary>
public class PublicBookingService : IPublicBookingService
{
    private readonly PhysioTracDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IAuditService _audit;
    private readonly IMemoryCache _cache;

    public PublicBookingService(PhysioTracDbContext db, IAvailabilityService availability, IAuditService audit, IMemoryCache cache)
    {
        _db = db;
        _availability = availability;
        _audit = audit;
        _cache = cache;
    }

    private bool Throttle(string bucket, string? ip, int limit, TimeSpan window)
    {
        var key = $"throttle:{bucket}:{ip ?? "unknown"}";
        var count = _cache.TryGetValue<int>(key, out var existing) ? existing : 0;
        if (count >= limit) return true;
        _cache.Set(key, count + 1, window);
        return false;
    }

    public async Task<PublicOrganizationDto?> GetOrganizationAsync(string slug, CancellationToken ct = default)
    {
        var (organization, config) = await TryResolveOrganizationAsync(slug, ct);
        if (organization is null || config is null) return null;
        return new PublicOrganizationDto(
            organization.Name, organization.Slug, organization.LogoPath,
            organization.AddressLine1, organization.AddressLine2, organization.City, organization.State, organization.ZipCode,
            organization.SupportPhone, organization.Timezone);
    }

    public async Task<IReadOnlyList<PublicLocationDto>> ListLocationsAsync(string slug, CancellationToken ct = default)
    {
        var (organization, config) = await TryResolveOrganizationAsync(slug, ct);
        if (organization is null || config is null) throw new BookingNotFoundException("This booking page is not available.");

        var locations = await _db.Locations.Where(l => l.OrganizationId == organization.Id && l.IsActive)
            .OrderBy(l => l.Name).ToListAsync(ct);
        return locations.Select(l => new PublicLocationDto(l.Id, l.Name, l.City, l.State, l.Timezone)).ToList();
    }

    public async Task<IReadOnlyList<PublicAppointmentTypeDto>> ListAppointmentTypesAsync(string slug, Guid? locationId = null, CancellationToken ct = default)
    {
        var (organization, config) = await TryResolveOrganizationAsync(slug, ct);
        if (organization is null || config is null) throw new BookingNotFoundException("This booking page is not available.");

        var query = _db.AppointmentTypes.Where(a => a.OrganizationId == organization.Id && a.IsActive && a.OnlineBookingEnabled);
        if (locationId is Guid locId)
        {
            var offeredTypeIds = await _db.ProviderAppointmentTypes
                .Where(l => l.Active && l.Provider!.IsActive && l.Provider.OnlineBookingEnabled && l.Provider.Locations.Any(loc => loc.Id == locId))
                .Select(l => l.AppointmentTypeId)
                .Distinct()
                .ToListAsync(ct);
            query = query.Where(a => offeredTypeIds.Contains(a.Id));
        }
        var types = await query.OrderBy(a => a.Name).ToListAsync(ct);
        return types.Select(a => new PublicAppointmentTypeDto(a.Id, a.Name, a.Description, a.DefaultDurationMinutes, a.Price, a.RequiresNewPatient)).ToList();
    }

    public async Task<IReadOnlyList<PublicProviderDto>> ListProvidersAsync(string slug, Guid locationId, Guid appointmentTypeId, CancellationToken ct = default)
    {
        var (organization, config) = await TryResolveOrganizationAsync(slug, ct);
        if (organization is null || config is null) throw new BookingNotFoundException("This booking page is not available.");

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId && l.OrganizationId == organization.Id && l.IsActive, ct)
            ?? throw new BookingNotFoundException("This location is not available for booking.");

        var providers = await _availability.EligibleProvidersAsync(organization.Id, location.Id, appointmentTypeId, ct);
        return providers.Select(p => new PublicProviderDto(p.Id, p.FullName, p.Credentials, p.Specialty, p.Bio)).ToList();
    }

    public async Task<PublicAvailabilityDto> GetAvailabilityAsync(
        string slug, Guid locationId, Guid appointmentTypeId, DateOnly date, Guid? providerId,
        string? callerIp, CancellationToken ct = default)
    {
        if (Throttle("availability", callerIp, 60, TimeSpan.FromSeconds(60)))
        {
            throw new RateLimitedException();
        }

        var (organization, config) = await TryResolveOrganizationAsync(slug, ct);
        if (organization is null || config is null) throw new BookingNotFoundException("This booking page is not available.");

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId && l.OrganizationId == organization.Id && l.IsActive, ct)
            ?? throw new BookingNotFoundException("This location is not available for booking.");
        _ = await _db.AppointmentTypes.FirstOrDefaultAsync(
                a => a.Id == appointmentTypeId && a.OrganizationId == organization.Id && a.IsActive && a.OnlineBookingEnabled, ct)
            ?? throw new BookingNotFoundException("This appointment type is not available for booking.");

        if (providerId is Guid pid)
        {
            _ = await _db.Providers.FirstOrDefaultAsync(
                    p => p.Id == pid && p.OrganizationId == organization.Id && p.IsActive && p.OnlineBookingEnabled, ct)
                ?? throw new BookingNotFoundException("This provider is not available for booking.");
        }

        var results = await _availability.GetAvailableSlotsAsync(organization.Id, locationId, appointmentTypeId, date, providerId, ct: ct);
        return new PublicAvailabilityDto(date, location.Timezone, results);
    }

    public async Task<PublicBookingResult> CreateBookingAsync(PublicBookingRequest request, string? callerIp, CancellationToken ct = default)
    {
        if (Throttle("booking", callerIp, 10, TimeSpan.FromSeconds(60)))
        {
            throw new RateLimitedException();
        }

        var (organization, config) = await TryResolveOrganizationAsync(request.OrganizationSlug, ct);
        if (organization is null || config is null) throw new BookingNotFoundException("This booking page is not available.");

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId && l.OrganizationId == organization.Id && l.IsActive, ct)
            ?? throw new BookingNotFoundException("This location is not available for booking.");
        var appointmentType = await _db.AppointmentTypes.FirstOrDefaultAsync(
                a => a.Id == request.AppointmentTypeId && a.OrganizationId == organization.Id && a.IsActive && a.OnlineBookingEnabled, ct)
            ?? throw new BookingNotFoundException("This appointment type is not available for booking.");

        if (request.IsNewPatient && !appointmentType.RequiresNewPatient && !config.AllowNewPatients)
        {
            throw new BookingValidationException("This organization is not currently accepting new patients online.");
        }
        if (!request.IsNewPatient && !config.AllowReturningPatients)
        {
            throw new BookingValidationException("Returning-patient booking is not currently available online.");
        }

        var providerCheck = await _db.Providers.FirstOrDefaultAsync(
                p => p.Id == request.ProviderId && p.OrganizationId == organization.Id && p.IsActive && p.OnlineBookingEnabled && p.UserId != null, ct)
            ?? throw new BookingNotFoundException("This provider is not available for booking.");
        if (!await _db.Providers.Where(p => p.Id == providerCheck.Id).AnyAsync(p => p.Locations.Any(l => l.Id == location.Id), ct))
        {
            throw new BookingNotFoundException("This provider does not work at the selected location.");
        }
        if (!await _db.ProviderAppointmentTypes.AnyAsync(l => l.ProviderId == providerCheck.Id && l.AppointmentTypeId == appointmentType.Id && l.Active, ct))
        {
            throw new BookingNotFoundException("This provider does not offer the selected appointment type.");
        }

        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Lock the provider row for the rest of this transaction so a
            // second, concurrent booking for the same provider blocks here
            // until this one commits or rolls back — closes the
            // double-booking race (matches `create_public_booking`).
            if (_db.Database.IsRealSqlServer())
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT Id FROM Providers WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", new object[] { providerCheck.Id }, ct);
            }

            var localDate = DateOnly.FromDateTime(request.StartDatetime.UtcDateTime);
            var slots = await _availability.GetProviderSlotsAsync(providerCheck.Id, location.Id, appointmentType.Id, localDate, ct: ct);
            var matchingSlot = slots.FirstOrDefault(s => s.Start == request.StartDatetime);
            if (matchingSlot is null)
            {
                throw new SlotNoLongerAvailableException();
            }

            var patient = await FindOrCreatePatientAsync(organization.Id, request.Patient, request.IsNewPatient, ct);

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                TherapistId = providerCheck.UserId!.Value,
                ProviderId = providerCheck.Id,
                LocationDetailId = location.Id,
                AppointmentTypeId = appointmentType.Id,
                Kind = appointmentType.DefaultKind ?? AppointmentKind.FollowUp,
                Status = AppointmentStatus.Scheduled,
                StartsAt = matchingSlot.Start,
                EndsAt = matchingSlot.End,
                IsHomeVisit = false,
                ReasonForVisit = request.ReasonForVisit?.Length > 240 ? request.ReasonForVisit[..240] : request.ReasonForVisit,
                BookingSource = BookingSource.PublicBooking,
                CreatedById = providerCheck.UserId.Value,
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAuditEventAsync(providerCheck.UserId, "appointment.created", nameof(Appointment), appointment.Id, organization.Id,
                patientId: patient.Id, ipAddress: callerIp,
                metadata: new { bookingSource = "public_booking", appointmentType = appointmentType.Name, location = location.Name }, ct: ct);

            if (transaction is not null) await transaction.CommitAsync(ct);

            return new PublicBookingResult(
                appointment.ConfirmationNumber, appointment.Id, appointment.StartsAt, appointment.EndsAt,
                providerCheck.FullName, location.Name, appointmentType.Name, organization.Name);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task<Patient> FindOrCreatePatientAsync(Guid organizationId, PublicPatientInfo info, bool isNewPatient, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(info.FirstName) || string.IsNullOrWhiteSpace(info.LastName))
        {
            throw new BookingValidationException("First name, last name, and date of birth are required.");
        }

        if (!isNewPatient && !string.IsNullOrWhiteSpace(info.Email))
        {
            var matches = await _db.Patients.Where(p =>
                    p.OrganizationId == organizationId &&
                    p.Email != null && p.Email.ToLower() == info.Email.ToLower() &&
                    p.LastName.ToLower() == info.LastName.ToLower() &&
                    p.DateOfBirth == info.DateOfBirth)
                .Take(2).ToListAsync(ct);
            if (matches.Count == 1)
            {
                var existing = matches[0];
                if (!string.IsNullOrWhiteSpace(info.Phone)) existing.Phone = info.Phone;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return existing;
            }
        }

        var patient = new Patient
        {
            OrganizationId = organizationId,
            FirstName = info.FirstName,
            LastName = info.LastName,
            DateOfBirth = info.DateOfBirth,
            Phone = info.Phone,
            Email = info.Email,
            Address = info.Address,
            EmergencyContact = info.EmergencyContact,
        };
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync(ct);
        return patient;
    }

    private async Task<(Organization? Organization, BookingConfiguration? Config)> TryResolveOrganizationAsync(string slug, CancellationToken ct)
    {
        var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Slug == slug, ct);
        if (organization is null || organization.ArchivedAt is not null || !organization.IsActive || organization.Status == OrganizationStatus.Suspended)
        {
            return (null, null);
        }

        var config = await _db.BookingConfigurations.FirstOrDefaultAsync(c => c.OrganizationId == organization.Id, ct);
        if (config is null)
        {
            config = new BookingConfiguration { OrganizationId = organization.Id };
            _db.BookingConfigurations.Add(config);
            await _db.SaveChangesAsync(ct);
        }
        if (!config.OnlineBookingEnabled)
        {
            return (organization, null);
        }
        return (organization, config);
    }
}

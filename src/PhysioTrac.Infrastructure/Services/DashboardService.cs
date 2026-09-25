using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Dashboards;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public DashboardService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new InvalidOperationException("The end of the date range cannot precede the start.");
        }
    }

    /// <summary>Therapist/Assistant callers everywhere in this service are
    /// narrowed to their own caseload/visits -- the same rule
    /// IAppointmentService.ListForRangeAsync already applies -- rather than
    /// being blocked outright, since a treating clinician legitimately
    /// needs their own productivity/schedule numbers.</summary>
    private static bool IsCaseloadNarrowed(ICurrentUser actor) => actor.Role is UserRole.Therapist or UserRole.Assistant;

    public async Task<NewPatientsDto> GetNewPatientsAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        ValidateRange(from, to);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var fromInstant = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toInstant = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var query = _db.Patients.Where(p =>
            p.OrganizationId == organization.Id && p.DeletedAt == null &&
            p.CreatedAt >= fromInstant && p.CreatedAt < toInstant);
        if (IsCaseloadNarrowed(actor))
        {
            query = query.Where(p => p.AssignedTherapistId == actor.UserId);
        }

        var createdDates = await query.Select(p => DateOnly.FromDateTime(p.CreatedAt.UtcDateTime)).ToListAsync(ct);
        var byDay = createdDates.GroupBy(d => d)
            .Select(g => new NewPatientsByDayDto(g.Key, g.Count()))
            .OrderBy(d => d.Date).ToList();

        return new NewPatientsDto(from, to, createdDates.Count, byDay);
    }

    public async Task<CancellationsNoShowsDto> GetCancellationsAndNoShowsAsync(
        ICurrentUser actor, DateOnly from, DateOnly to, Guid? providerId = null, Guid? locationId = null, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        ValidateRange(from, to);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var statuses = await AppointmentsInRangeAsync(actor, organization.Id, from, to, providerId, locationId, ct)
            .Select(a => a.Status).ToListAsync(ct);

        var total = statuses.Count;
        var completed = statuses.Count(s => s == AppointmentStatus.Completed);
        var cancelled = statuses.Count(s => s == AppointmentStatus.Cancelled);
        var noShow = statuses.Count(s => s == AppointmentStatus.NoShow);

        return new CancellationsNoShowsDto(
            from, to, total, completed, cancelled, noShow,
            total == 0 ? 0 : Math.Round(100m * cancelled / total, 1),
            total == 0 ? 0 : Math.Round(100m * noShow / total, 1));
    }

    public async Task<ProviderProductivityDto> GetProviderProductivityAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        ValidateRange(from, to);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var visitQuery = AppointmentsInRangeAsync(actor, organization.Id, from, to, null, null, ct)
            .Where(a => a.Status == AppointmentStatus.Completed && a.ProviderId != null);
        var visits = await visitQuery.ToListAsync(ct);

        var chargeQuery = _db.Charges.Where(c =>
            c.OrganizationId == organization.Id && c.Status != ChargeStatus.Void &&
            c.ServiceDate >= from && c.ServiceDate <= to);
        if (IsCaseloadNarrowed(actor))
        {
            // A Charge has no TherapistId of its own -- only ProviderId --
            // so narrow via the same providers this actor's own visits used.
            var ownProviderIds = visits.Select(v => v.ProviderId!.Value).Distinct().ToList();
            chargeQuery = chargeQuery.Where(c => ownProviderIds.Contains(c.ProviderId));
        }
        var charges = await chargeQuery.ToListAsync(ct);

        var providerIds = visits.Select(v => v.ProviderId!.Value).Union(charges.Select(c => c.ProviderId)).Distinct().ToList();
        var providers = await _db.Providers.Where(p => providerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var rows = providerIds.Select(id => new ProviderProductivityRowDto(
            id,
            providers.TryGetValue(id, out var p) ? $"{p.FirstName} {p.LastName}" : "Unknown provider",
            visits.Count(v => v.ProviderId == id),
            charges.Where(c => c.ProviderId == id).Sum(c => c.Units),
            charges.Where(c => c.ProviderId == id).Sum(c => c.ChargeAmount)))
            .OrderByDescending(r => r.CompletedVisits).ToList();

        return new ProviderProductivityDto(from, to, rows);
    }

    public async Task<VisitsAndRetentionDto> GetVisitsAndRetentionAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        ValidateRange(from, to);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var completed = await AppointmentsInRangeAsync(actor, organization.Id, from, to, null, null, ct)
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Select(a => new { a.PatientId, a.StartsAt })
            .ToListAsync(ct);

        var totalVisits = completed.Count;
        var uniquePatients = completed.Select(v => v.PatientId).Distinct().Count();

        // Split the range into two equal halves by day count (see
        // VisitsAndRetentionDto's own doc comment for the exact methodology).
        var totalDays = to.DayNumber - from.DayNumber + 1;
        var midpoint = from.AddDays(totalDays / 2);

        var firstHalfPatients = completed.Where(v => DateOnly.FromDateTime(v.StartsAt.UtcDateTime) < midpoint)
            .Select(v => v.PatientId).Distinct().ToHashSet();
        var secondHalfPatients = completed.Where(v => DateOnly.FromDateTime(v.StartsAt.UtcDateTime) >= midpoint)
            .Select(v => v.PatientId).Distinct().ToHashSet();
        var retained = firstHalfPatients.Count(p => secondHalfPatients.Contains(p));

        return new VisitsAndRetentionDto(
            from, to, totalVisits, uniquePatients, firstHalfPatients.Count, retained,
            firstHalfPatients.Count == 0 ? 0 : Math.Round(100m * retained / firstHalfPatients.Count, 1));
    }

    public async Task<ReferralSourcesDto> GetReferralSourcesAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.AllStaff);
        ValidateRange(from, to);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var fromInstant = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toInstant = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var newPatients = await _db.Patients
            .Where(p => p.OrganizationId == organization.Id && p.DeletedAt == null && p.CreatedAt >= fromInstant && p.CreatedAt < toInstant)
            .Select(p => p.ReferringProviderId)
            .ToListAsync(ct);

        var referringProviderIds = newPatients.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var referringProviders = await _db.ReferringProviders.Where(r => referringProviderIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        var rows = newPatients.GroupBy(id => id)
            .Select(g => new ReferralSourceRowDto(
                g.Key,
                g.Key is Guid rid && referringProviders.TryGetValue(rid, out var r) ? r.FullName : "No referral source recorded",
                g.Count()))
            .OrderByDescending(r => r.NewPatientCount).ToList();

        return new ReferralSourcesDto(from, to, rows);
    }

    public async Task<LocationPerformanceDto> GetLocationPerformanceAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Billing);
        ValidateRange(from, to);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var visits = await _db.Appointments
            .Join(_db.Patients.Where(p => p.OrganizationId == organization.Id), a => a.PatientId, p => p.Id, (a, p) => a)
            .Where(a => a.Status == AppointmentStatus.Completed && a.StartsAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                && a.StartsAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Select(a => a.LocationDetailId)
            .ToListAsync(ct);

        var charges = await _db.Charges
            .Where(c => c.OrganizationId == organization.Id && c.Status != ChargeStatus.Void && c.ServiceDate >= from && c.ServiceDate <= to)
            .Select(c => new { c.LocationId, c.ChargeAmount })
            .ToListAsync(ct);

        var fromInstant = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toInstant = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var newPatientsByLocation = await _db.Patients
            .Where(p => p.OrganizationId == organization.Id && p.DeletedAt == null && p.CreatedAt >= fromInstant && p.CreatedAt < toInstant)
            .Select(p => p.PrimaryLocationId)
            .ToListAsync(ct);

        var locationIds = visits.Where(id => id is not null).Select(id => id!.Value)
            .Union(charges.Where(c => c.LocationId is not null).Select(c => c.LocationId!.Value))
            .Union(newPatientsByLocation.Where(id => id is not null).Select(id => id!.Value))
            .Distinct().ToList();
        var locations = await _db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);

        // Includes a "no location recorded" row (key null) alongside every
        // known location, rather than silently dropping activity that
        // predates consistent location tagging.
        var allKeys = locationIds.Cast<Guid?>().Append(null).Distinct();
        var rows = allKeys.Select(id => new LocationPerformanceRowDto(
            id,
            id is Guid lid && locations.TryGetValue(lid, out var l) ? l.Name : "No location recorded",
            visits.Count(v => v == id),
            charges.Where(c => c.LocationId == id).Sum(c => c.ChargeAmount),
            newPatientsByLocation.Count(n => n == id)))
            .Where(r => r.Visits > 0 || r.Revenue > 0 || r.NewPatients > 0)
            .OrderByDescending(r => r.Revenue).ToList();

        return new LocationPerformanceDto(from, to, rows);
    }

    /// <summary>Shared date-range/role-narrowing appointment query -- the
    /// same shape IAppointmentService.ListForRangeAsync uses (org-scoped via
    /// a Patients join, Therapist/Assistant narrowed to their own
    /// TherapistId), reimplemented here rather than reused because that
    /// method returns a materialized list while every dashboard method above
    /// needs to compose further Where/Select clauses first.</summary>
    private IQueryable<Appointment> AppointmentsInRangeAsync(
        ICurrentUser actor, Guid organizationId, DateOnly from, DateOnly to, Guid? providerId, Guid? locationId, CancellationToken ct)
    {
        var fromInstant = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toInstant = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var query = _db.Appointments
            .Join(_db.Patients.Where(p => p.OrganizationId == organizationId), a => a.PatientId, p => p.Id, (a, p) => a)
            .Where(a => a.StartsAt >= fromInstant && a.StartsAt < toInstant);

        if (IsCaseloadNarrowed(actor))
        {
            query = query.Where(a => a.TherapistId == actor.UserId);
        }
        if (providerId is Guid pid)
        {
            query = query.Where(a => a.ProviderId == pid);
        }
        if (locationId is Guid lid)
        {
            query = query.Where(a => a.LocationDetailId == lid);
        }

        return query;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/providers")]
[Authorize]
public class ProvidersController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ProvidersController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var providers = await _db.Providers.Where(p => p.OrganizationId == organization.Id)
                .Include(p => p.Locations)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(providers.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var provider = await LoadProviderInOrgAsync(id, HttpContext.RequestAborted);
            return Ok(ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var problem = await ValidateAsync(organization.Id, null, request.FirstName, request.LastName, request.NpiNumber, request.Discipline,
                request.UserId, HttpContext.RequestAborted);
            if (problem is not null) return problem;

            var provider = new Provider
            {
                OrganizationId = organization.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Specialty = Clean(request.Specialty),
                Credentials = Clean(request.Credentials),
                NpiNumber = Clean(request.NpiNumber),
                Discipline = request.Discipline,
                OnlineBookingEnabled = request.OnlineBookingEnabled,
                UserId = request.UserId,
            };
            await AssignLocationsAsync(provider, organization.Id, request.LocationIds, HttpContext.RequestAborted);

            _db.Providers.Add(provider);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = provider.Id }, ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProviderRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var provider = await LoadProviderInOrgAsync(id, HttpContext.RequestAborted);

            var discipline = request.Discipline ?? provider.Discipline;
            var userId = request.UpdateLogin ? request.UserId : provider.UserId;
            var problem = await ValidateAsync(provider.OrganizationId, provider.Id, request.FirstName, request.LastName, request.NpiNumber,
                discipline, userId, HttpContext.RequestAborted);
            if (problem is not null) return problem;

            provider.FirstName = request.FirstName.Trim();
            provider.LastName = request.LastName.Trim();
            provider.Specialty = Clean(request.Specialty);
            provider.Credentials = Clean(request.Credentials);
            provider.NpiNumber = Clean(request.NpiNumber);
            provider.IsActive = request.IsActive;
            provider.Discipline = discipline;
            if (request.OnlineBookingEnabled is bool online) provider.OnlineBookingEnabled = online;
            provider.UserId = userId;
            provider.UpdatedAt = DateTimeOffset.UtcNow;

            if (request.LocationIds is not null)
            {
                provider.Locations.Clear();
                await AssignLocationsAsync(provider, provider.OrganizationId, request.LocationIds, HttpContext.RequestAborted);
            }

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    /// <summary>Deletes a provider that was added by mistake. A provider with
    /// any history (appointments, notes, charges, waitlist entries) can't be
    /// deleted -- deactivate them instead, so the record stays complete.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var ct = HttpContext.RequestAborted;
            var provider = await LoadProviderInOrgAsync(id, ct);

            var inUse = new List<string>();
            if (await _db.Appointments.AnyAsync(a => a.ProviderId == id, ct)) inUse.Add("appointments");
            if (await _db.AppointmentSeries.AnyAsync(a => a.ProviderId == id, ct)) inUse.Add("recurring series");
            if (await _db.ClinicalNotes.AnyAsync(n => n.TreatingProviderId == id || n.SupervisingProviderId == id, ct)) inUse.Add("clinical notes");
            if (await _db.Charges.AnyAsync(c => c.ProviderId == id, ct)) inUse.Add("charges");
            if (await _db.Waitlists.AnyAsync(w => w.ProviderId == id, ct)) inUse.Add("waitlist entries");
            if (inUse.Count > 0)
            {
                return Conflict(new
                {
                    detail = $"{provider.FullName} has {string.Join(", ", inUse)} and can't be deleted. Deactivate them instead; their history is kept.",
                });
            }

            _db.ProviderAvailabilities.RemoveRange(await _db.ProviderAvailabilities.Where(a => a.ProviderId == id).ToListAsync(ct));
            _db.ProviderTimeOffs.RemoveRange(await _db.ProviderTimeOffs.Where(t => t.ProviderId == id).ToListAsync(ct));
            _db.ProviderAppointmentTypes.RemoveRange(await _db.ProviderAppointmentTypes.Where(t => t.ProviderId == id).ToListAsync(ct));
            _db.ProviderLicenses.RemoveRange(await _db.ProviderLicenses.Where(l => l.ProviderId == id).ToListAsync(ct));
            provider.Locations.Clear();
            _db.Providers.Remove(provider);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    /// <summary>Clinical staff logins in this organization that aren't linked
    /// to another provider (plus the one linked to <paramref name="providerId"/>).</summary>
    [HttpGet("linkable-users")]
    public async Task<IActionResult> LinkableUsers([FromQuery] Guid? providerId = null)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var ct = HttpContext.RequestAborted;
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
            var taken = await _db.Providers
                .Where(p => p.OrganizationId == organization.Id && p.UserId != null && p.Id != providerId)
                .Select(p => p.UserId!.Value).ToListAsync(ct);
            var users = await _db.Users
                .Where(u => u.OrganizationId == organization.Id && LinkableRoles.Contains(u.Role) && !taken.Contains(u.Id))
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName, u.Role })
                .ToListAsync(ct);
            return Ok(users.Select(u => new LinkableUserDto(u.Id, $"{u.FirstName} {u.LastName}".Trim(), u.UserName ?? "", u.Role.ToString())));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    /// <summary>Roles that see patients and so can be a provider's login.</summary>
    private static readonly UserRole[] LinkableRoles = [UserRole.Therapist, UserRole.Assistant, UserRole.Director, UserRole.Admin];

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<IActionResult?> ValidateAsync(Guid organizationId, Guid? providerId, string? firstName, string? lastName, string? npi,
        ProviderDiscipline discipline, Guid? userId, CancellationToken ct)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(firstName)) errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) errors.Add("Last name is required.");
        if ((firstName?.Trim().Length ?? 0) > 100 || (lastName?.Trim().Length ?? 0) > 100) errors.Add("Names are 100 characters at most.");
        if (!Enum.IsDefined(discipline)) errors.Add("Choose PT, PTA or Other.");
        var cleanNpi = Clean(npi);
        if (cleanNpi is not null && !System.Text.RegularExpressions.Regex.IsMatch(cleanNpi, "^[0-9]{10}$")) errors.Add("An NPI is 10 digits.");
        if (errors.Count > 0) return UnprocessableEntity(new { detail = string.Join(" ", errors), errors });

        if (cleanNpi is not null &&
            await _db.Providers.AnyAsync(p => p.OrganizationId == organizationId && p.NpiNumber == cleanNpi && p.Id != providerId, ct))
            return Conflict(new { detail = "Another provider in this clinic already has that NPI." });

        if (userId is Guid uid)
        {
            var user = await _db.Users.Where(u => u.Id == uid).Select(u => new { u.OrganizationId, u.Role }).FirstOrDefaultAsync(ct);
            if (user is null || user.OrganizationId != organizationId || !LinkableRoles.Contains(user.Role))
                return UnprocessableEntity(new { detail = "Choose a clinical staff login from this clinic." });
            if (await _db.Providers.AnyAsync(p => p.UserId == uid && p.Id != providerId, ct))
                return Conflict(new { detail = "That login is already linked to another provider." });
        }
        return null;
    }

    private async Task AssignLocationsAsync(Provider provider, Guid organizationId, IReadOnlyList<Guid>? locationIds, CancellationToken ct)
    {
        if (locationIds is not { Count: > 0 }) return;

        // Silently drops any id that isn't one of the caller's own org's
        // locations -- the same "never trust a client-supplied id across a
        // tenant boundary" rule every other assignment in this Api follows.
        var locations = await _db.Locations
            .Where(l => l.OrganizationId == organizationId && locationIds.Contains(l.Id))
            .ToListAsync(ct);
        foreach (var location in locations) provider.Locations.Add(location);
    }

    private async Task<Provider> LoadProviderInOrgAsync(Guid id, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        var provider = await _db.Providers.Include(p => p.Locations).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Provider was not found.");
        if (provider.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Provider was not found.");
        }
        return provider;
    }

    private static ProviderDto ToDto(Provider p) => new(
        p.Id, p.FirstName, p.LastName, p.FullName, p.Specialty, p.Credentials, p.NpiNumber,
        p.IsActive, p.OnlineBookingEnabled, p.Locations.Select(l => l.Id).ToList(), p.Discipline, p.UserId is not null, p.UserId);
}

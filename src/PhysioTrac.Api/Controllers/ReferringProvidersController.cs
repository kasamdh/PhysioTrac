using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/referring-providers")]
[Authorize]
public class ReferringProvidersController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ReferringProvidersController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var providers = await _db.ReferringProviders.Where(r => r.OrganizationId == organization.Id)
                .OrderBy(r => r.LastName).ThenBy(r => r.FirstName)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(providers.Select(ToDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReferringProviderRequest request)
    {
        try
        {
            // Scheduling roles too, not just Clinical -- front desk is often
            // the one entering a new referral source during intake.
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return UnprocessableEntity(new { detail = "First and last name are required." });
            }

            var provider = new ReferringProvider
            {
                OrganizationId = organization.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Npi = request.Npi,
                Specialty = request.Specialty,
                Phone = request.Phone,
                Fax = request.Fax,
                Email = request.Email,
                Address = request.Address,
                CreatedById = _currentUser.UserId,
            };
            _db.ReferringProviders.Add(provider);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(provider));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    private static ReferringProviderDto ToDto(ReferringProvider r) => new(
        r.Id, r.FullName, r.FirstName, r.LastName, r.Npi, r.Specialty, r.Phone, r.Fax, r.Email, r.IsActive);
}

public record ReferringProviderDto(
    Guid Id, string FullName, string FirstName, string LastName, string? Npi,
    string? Specialty, string? Phone, string? Fax, string? Email, bool IsActive);

public record CreateReferringProviderRequest(
    string FirstName, string LastName, string? Npi, string? Specialty,
    string? Phone, string? Fax, string? Email, string? Address);

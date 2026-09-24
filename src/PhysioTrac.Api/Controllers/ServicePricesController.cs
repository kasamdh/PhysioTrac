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
[Route("api/v1/service-prices")]
[Authorize]
public class ServicePricesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ServicePricesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? locationId = null)
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var query = _db.ServicePrices.Where(s => s.OrganizationId == organization.Id);
            if (locationId is Guid loc)
            {
                // Both an organization-wide default and a location-specific
                // override can exist for the same CPT code -- return both so
                // a client can show which one currently wins for this location.
                query = query.Where(s => s.LocationId == loc || s.LocationId == null);
            }
            var prices = await query.OrderBy(s => s.CptCode).ThenBy(s => s.LocationId).ToListAsync(HttpContext.RequestAborted);
            return Ok(prices);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServicePriceRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            // Mirrors ServicePrice.clean(): the two tags are mutually
            // exclusive, and a deposit is only meaningful on a
            // home-visit-kind row.
            if (request.HomeVisitKind is not null && request.IsHomeVisitTravelFee)
            {
                return UnprocessableEntity(new { detail = "A price row can be tagged as a home-visit kind or the travel fee, not both." });
            }
            if (request.DepositAmount is decimal deposit)
            {
                if (request.IsHomeVisitTravelFee || request.HomeVisitKind is null)
                {
                    return UnprocessableEntity(new { detail = "A deposit can only be set on a home-visit-kind price." });
                }
                if (deposit <= 0)
                {
                    return UnprocessableEntity(new { detail = "Enter a deposit amount greater than zero." });
                }
            }
            if (request.LocationId is Guid locationId)
            {
                var locationExists = await _db.Locations.AnyAsync(
                    l => l.Id == locationId && l.OrganizationId == organization.Id, HttpContext.RequestAborted);
                if (!locationExists)
                {
                    return NotFound(new { detail = "Location was not found." });
                }
            }

            var price = new ServicePrice
            {
                OrganizationId = organization.Id,
                LocationId = request.LocationId,
                CptCode = request.CptCode,
                Label = request.Label,
                Price = request.Price,
                HomeVisitKind = request.HomeVisitKind,
                IsHomeVisitTravelFee = request.IsHomeVisitTravelFee,
                DepositAmount = request.DepositAmount,
                CreatedById = _currentUser.UserId,
            };
            _db.ServicePrices.Add(price);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, price);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record CreateServicePriceRequest(
    string CptCode, string Label, decimal Price, Guid? LocationId,
    PhysioTrac.Domain.Enums.AppointmentKind? HomeVisitKind, bool IsHomeVisitTravelFee, decimal? DepositAmount);

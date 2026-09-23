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
    public async Task<IActionResult> List()
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var prices = await _db.ServicePrices.Where(s => s.OrganizationId == organization.Id)
                .OrderBy(s => s.CptCode).ToListAsync(HttpContext.RequestAborted);
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

            var price = new ServicePrice
            {
                OrganizationId = organization.Id,
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
    string CptCode, string Label, decimal Price,
    PhysioTrac.Domain.Enums.AppointmentKind? HomeVisitKind, bool IsHomeVisitTravelFee, decimal? DepositAmount);

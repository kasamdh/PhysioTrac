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
[Route("api/v1/payers")]
[Authorize]
public class PayersController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public PayersController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var payers = await _db.Payers.Where(p => p.OrganizationId == organization.Id)
                .OrderBy(p => p.Name).ToListAsync(HttpContext.RequestAborted);
            return Ok(payers);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayerRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var payer = new Payer
            {
                OrganizationId = organization.Id,
                Name = request.Name,
                PayerId = request.PayerId,
                ElectronicPayerId = request.ElectronicPayerId,
                TimelyFilingDays = request.TimelyFilingDays ?? 90,
                AuthorizationRequired = request.AuthorizationRequired,
                AuthorizationNotes = request.AuthorizationNotes,
                Notes = request.Notes,
                CreatedById = _currentUser.UserId,
            };
            _db.Payers.Add(payer);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, payer);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record CreatePayerRequest(string Name, string? PayerId, string? ElectronicPayerId, int? TimelyFilingDays, bool AuthorizationRequired, string? AuthorizationNotes, string? Notes);

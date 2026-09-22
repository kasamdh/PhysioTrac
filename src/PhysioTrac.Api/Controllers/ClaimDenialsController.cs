using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/claim-denials")]
[Authorize]
public class ClaimDenialsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ClaimDenialsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DenialResolution? resolution)
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var query = _db.ClaimDenials.Where(d => d.OrganizationId == organization.Id);
            if (resolution is DenialResolution res) query = query.Where(d => d.Resolution == res);
            var denials = await query.OrderBy(d => d.Resolution).ThenBy(d => d.DueDate).ToListAsync(HttpContext.RequestAborted);
            return Ok(denials);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClaimDenialRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == request.ClaimId && c.OrganizationId == organization.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Claim was not found.");

            var denial = new ClaimDenial
            {
                OrganizationId = organization.Id,
                PatientId = claim.PatientId,
                ClaimId = claim.Id,
                DenialCode = request.DenialCode,
                DenialReason = request.DenialReason,
                DeniedOn = request.DeniedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
                OwnerId = request.OwnerId,
                DueDate = request.DueDate,
                CreatedById = _currentUser.UserId,
            };
            _db.ClaimDenials.Add(denial);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, denial);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveClaimDenialRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var denial = await _db.ClaimDenials.FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == organization.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Denial was not found.");

            denial.Resolution = request.Resolution;
            denial.AppealStatus = request.AppealStatus ?? denial.AppealStatus;
            denial.ActionNotes = request.ActionNotes ?? denial.ActionNotes;
            if (request.Resolution != DenialResolution.Open)
            {
                denial.ResolvedAt = DateTimeOffset.UtcNow;
            }
            denial.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(denial);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }
}

public record CreateClaimDenialRequest(Guid ClaimId, string? DenialCode, string DenialReason, DateOnly? DeniedOn, Guid? OwnerId, DateOnly? DueDate);
public record ResolveClaimDenialRequest(DenialResolution Resolution, AppealStatus? AppealStatus, string? ActionNotes);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>A payer's contracted/allowed amount per CPT code -- see
/// PayerFeeScheduleItem's own doc comment. Nested under the owning payer
/// since a fee schedule item is meaningless without one.</summary>
[ApiController]
[Route("api/v1/payers/{payerId:guid}/fee-schedule")]
[Authorize]
public class PayerFeeScheduleController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public PayerFeeScheduleController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    private async Task<Payer?> LoadPayerInOrgAsync(Guid payerId, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        return await _db.Payers.FirstOrDefaultAsync(p => p.Id == payerId && p.OrganizationId == organization.Id, ct);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid payerId)
    {
        try
        {
            var payer = await LoadPayerInOrgAsync(payerId, HttpContext.RequestAborted);
            if (payer is null) return NotFound(new { detail = "Payer was not found." });

            var items = await _db.PayerFeeScheduleItems.Where(f => f.PayerId == payer.Id)
                .OrderBy(f => f.CptCode).ToListAsync(HttpContext.RequestAborted);
            return Ok(items);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(Guid payerId, [FromBody] UpsertPayerFeeScheduleItemRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var payer = await LoadPayerInOrgAsync(payerId, HttpContext.RequestAborted);
            if (payer is null) return NotFound(new { detail = "Payer was not found." });

            if (string.IsNullOrWhiteSpace(request.CptCode))
            {
                return UnprocessableEntity(new { detail = "A CPT code is required." });
            }
            if (request.AllowedAmount < 0)
            {
                return UnprocessableEntity(new { detail = "Allowed amount cannot be negative." });
            }

            var cptCode = request.CptCode.Trim().ToUpperInvariant();
            var existing = await _db.PayerFeeScheduleItems.FirstOrDefaultAsync(
                f => f.PayerId == payer.Id && f.CptCode == cptCode, HttpContext.RequestAborted);
            if (existing is not null)
            {
                existing.AllowedAmount = request.AllowedAmount;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
                return Ok(existing);
            }

            var item = new PayerFeeScheduleItem
            {
                PayerId = payer.Id,
                CptCode = cptCode,
                AllowedAmount = request.AllowedAmount,
                CreatedById = _currentUser.UserId,
            };
            _db.PayerFeeScheduleItems.Add(item);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), new { payerId }, item);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record UpsertPayerFeeScheduleItemRequest(string CptCode, decimal AllowedAmount);

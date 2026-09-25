using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/superbills")]
[Authorize]
public class SuperbillsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public SuperbillsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db, IAuditService audit)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
        _audit = audit;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var superbills = await _db.Superbills.Where(s => s.PatientId == patient.Id)
                .OrderByDescending(s => s.ServiceDate).ToListAsync(HttpContext.RequestAborted);
            return Ok(superbills);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSuperbillRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, request.PatientId, ct: HttpContext.RequestAborted);

            List<Charge> charges = new();
            if (request.ChargeIds is { Count: > 0 })
            {
                charges = await _db.Charges.Where(c => request.ChargeIds.Contains(c.Id) && c.PatientId == patient.Id).ToListAsync(HttpContext.RequestAborted);
                if (charges.Any(c => c.ClaimId is not null || c.SuperbillId is not null))
                {
                    return UnprocessableEntity(new { detail = "A charge can be billed to insurance or cash-pay, not both, and cannot be reused." });
                }
            }

            var superbill = new Superbill
            {
                PatientId = patient.Id,
                ClinicianId = request.ClinicianId,
                ServiceDate = request.ServiceDate,
                CodesJson = JsonSerializer.Serialize(request.Codes ?? new List<string>()),
                Amount = request.Amount,
            };
            _db.Superbills.Add(superbill);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            foreach (var charge in charges) charge.SuperbillId = superbill.Id;
            if (charges.Count > 0) await _db.SaveChangesAsync(HttpContext.RequestAborted);

            // Superbill has no OrganizationId of its own, so it isn't
            // covered by EntityChangeAuditInterceptor.
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "superbill.created", nameof(Superbill), superbill.Id,
                organization.Id, patientId: patient.Id, metadata: new { amount = superbill.Amount, chargeCount = charges.Count }, ct: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(ListForPatient), new { patientId = patient.Id }, superbill);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record CreateSuperbillRequest(Guid PatientId, Guid ClinicianId, DateOnly ServiceDate, List<string>? Codes, decimal Amount, IReadOnlyList<Guid>? ChargeIds);

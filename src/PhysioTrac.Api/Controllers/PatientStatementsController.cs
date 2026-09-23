using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Generates a patient billing statement — only the generation
/// event and a balance snapshot are stored. Simplification vs. the
/// original: the snapshot sums each of the patient's claim balances as of
/// now, not filtered to activity on/before the statement date (that needs
/// `billing_services.build_patient_statement_data`, not ported).</summary>
[ApiController]
[Route("api/v1/patient-statements")]
[Authorize]
public class PatientStatementsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IClaimService _claims;
    private readonly PhysioTracDbContext _db;

    public PatientStatementsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IClaimService claims, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _claims = claims;
        _db = db;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var statements = await _db.PatientStatements.Where(s => s.PatientId == patient.Id)
                .OrderByDescending(s => s.StatementDate).ToListAsync(HttpContext.RequestAborted);
            return Ok(statements);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost("patient/{patientId:guid}/generate")]
    public async Task<IActionResult> Generate(Guid patientId, [FromBody] GenerateStatementRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var claims = await _claims.ListForPatientAsync(patient.Id, _currentUser, HttpContext.RequestAborted);
            decimal balance = 0;
            foreach (var claim in claims)
            {
                var totals = await _claims.GetTotalsAsync(claim.Id, _currentUser, HttpContext.RequestAborted);
                balance += totals.Balance;
            }

            var statement = new PatientStatement
            {
                OrganizationId = organization.Id,
                PatientId = patient.Id,
                DueDate = request.DueDate,
                BalanceAtGeneration = balance,
                GeneratedById = _currentUser.UserId,
            };
            _db.PatientStatements.Add(statement);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForPatient), new { patientId = patient.Id }, statement);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record GenerateStatementRequest(DateOnly DueDate);

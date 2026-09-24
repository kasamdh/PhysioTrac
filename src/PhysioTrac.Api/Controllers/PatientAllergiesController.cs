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

/// <summary>Every route resolves the patient via RequirePatientAccessAsync
/// first -- tenant/caseload scoping for allergy data is exactly the same as
/// for the chart itself, never looser.</summary>
[ApiController]
[Route("api/v1/patients/{patientId:guid}/allergies")]
[Authorize]
public class PatientAllergiesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public PatientAllergiesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId, [FromQuery] bool includeInactive = false)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var query = _db.PatientAllergies.Where(a => a.PatientId == patient.Id);
            if (!includeInactive) query = query.Where(a => a.IsActive);

            var allergies = await query.OrderByDescending(a => a.CreatedAt).ToListAsync(HttpContext.RequestAborted);
            return Ok(allergies.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] CreatePatientAllergyRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Clinical);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.Allergen))
            {
                return UnprocessableEntity(new { detail = "An allergen is required." });
            }

            var allergy = new PatientAllergy
            {
                PatientId = patient.Id,
                Allergen = request.Allergen.Trim(),
                Reaction = request.Reaction,
                Severity = request.Severity,
                Notes = request.Notes,
                RecordedById = _currentUser.UserId,
            };
            _db.PatientAllergies.Add(allergy);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), new { patientId }, ToDto(allergy));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPatch("{allergyId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid patientId, Guid allergyId, [FromBody] DeactivateAllergyRequest? request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Clinical);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var allergy = await _db.PatientAllergies.FirstOrDefaultAsync(a => a.Id == allergyId && a.PatientId == patient.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Allergy record was not found.");

            allergy.IsActive = false;
            allergy.Notes = request?.Reason is { Length: > 0 } reason ? $"{allergy.Notes}\n[deactivated: {reason}]".Trim() : allergy.Notes;
            allergy.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(allergy));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private static PatientAllergyDto ToDto(PatientAllergy a) => new(
        a.Id, a.PatientId, a.Allergen, a.Reaction, a.Severity, a.Notes, a.IsActive, a.CreatedAt);
}

public record PatientAllergyDto(
    Guid Id, Guid PatientId, string Allergen, string? Reaction, AllergySeverity Severity, string? Notes, bool IsActive, DateTimeOffset CreatedAt);

public record CreatePatientAllergyRequest(string Allergen, string? Reaction, AllergySeverity Severity, string? Notes);

public record DeactivateAllergyRequest(string? Reason);

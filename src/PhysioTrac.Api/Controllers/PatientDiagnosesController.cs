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

/// <summary>Structured ICD-10 diagnoses on a patient's chart -- additive to
/// (not a replacement for) the free-text Patient.Diagnoses field; see
/// PatientDiagnosis's own doc comment.</summary>
[ApiController]
[Route("api/v1/patients/{patientId:guid}/diagnoses")]
[Authorize]
public class PatientDiagnosesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public PatientDiagnosesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db, IAuditService audit)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var diagnoses = await _db.PatientDiagnoses
                .Include(d => d.DiagnosisCode)
                .Where(d => d.PatientId == patient.Id)
                .OrderByDescending(d => d.IsPrimary).ThenByDescending(d => d.DiagnosedDate)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(diagnoses.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] CreatePatientDiagnosisRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Clinical);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);

            var diagnosisCode = await _db.DiagnosisCodes.FirstOrDefaultAsync(c => c.Id == request.DiagnosisCodeId, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Diagnosis code was not found.");

            var diagnosis = new PatientDiagnosis
            {
                PatientId = patient.Id,
                DiagnosisCodeId = diagnosisCode.Id,
                IsPrimary = request.IsPrimary,
                DiagnosedDate = request.DiagnosedDate,
                Notes = request.Notes,
            };
            _db.PatientDiagnoses.Add(diagnosis);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "patient_diagnosis.created", nameof(PatientDiagnosis), diagnosis.Id,
                organization.Id, patientId: patient.Id, metadata: new { diagnosisCode = diagnosisCode.Code }, ct: HttpContext.RequestAborted);

            diagnosis.DiagnosisCode = diagnosisCode;
            return CreatedAtAction(nameof(List), new { patientId }, ToDto(diagnosis));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPatch("{diagnosisId:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid patientId, Guid diagnosisId)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Clinical);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var diagnosis = await _db.PatientDiagnoses.Include(d => d.DiagnosisCode)
                .FirstOrDefaultAsync(d => d.Id == diagnosisId && d.PatientId == patient.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Diagnosis was not found.");

            diagnosis.ResolvedDate = DateOnly.FromDateTime(DateTime.UtcNow);
            diagnosis.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "patient_diagnosis.resolved", nameof(PatientDiagnosis), diagnosis.Id,
                organization.Id, patientId: patient.Id, ct: HttpContext.RequestAborted);

            return Ok(ToDto(diagnosis));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private static PatientDiagnosisDto ToDto(PatientDiagnosis d) => new(
        d.Id, d.PatientId, d.DiagnosisCodeId, d.DiagnosisCode!.Code, d.DiagnosisCode.Description,
        d.IsPrimary, d.DiagnosedDate, d.ResolvedDate, d.IsResolved, d.Notes);
}

public record PatientDiagnosisDto(
    Guid Id, Guid PatientId, Guid DiagnosisCodeId, string Code, string Description,
    bool IsPrimary, DateOnly? DiagnosedDate, DateOnly? ResolvedDate, bool IsResolved, string? Notes);

public record CreatePatientDiagnosisRequest(Guid DiagnosisCodeId, bool IsPrimary, DateOnly? DiagnosedDate, string? Notes);

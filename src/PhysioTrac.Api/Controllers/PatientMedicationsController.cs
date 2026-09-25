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
[Route("api/v1/patients/{patientId:guid}/medications")]
[Authorize]
public class PatientMedicationsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public PatientMedicationsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db, IAuditService audit)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId, [FromQuery] bool includeInactive = false)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var query = _db.PatientMedications.Where(m => m.PatientId == patient.Id);
            if (!includeInactive) query = query.Where(m => m.IsActive);

            var medications = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(HttpContext.RequestAborted);
            return Ok(medications.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] CreatePatientMedicationRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Clinical);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return UnprocessableEntity(new { detail = "A medication name is required." });
            }

            var medication = new PatientMedication
            {
                PatientId = patient.Id,
                Name = request.Name.Trim(),
                Dosage = request.Dosage,
                Frequency = request.Frequency,
                PrescribingProvider = request.PrescribingProvider,
                StartDate = request.StartDate,
                Notes = request.Notes,
                RecordedById = _currentUser.UserId,
            };
            _db.PatientMedications.Add(medication);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "patient_medication.created", nameof(PatientMedication), medication.Id,
                organization.Id, patientId: patient.Id, ct: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(List), new { patientId }, ToDto(medication));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPatch("{medicationId:guid}/discontinue")]
    public async Task<IActionResult> Discontinue(Guid patientId, Guid medicationId)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Clinical);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var medication = await _db.PatientMedications.FirstOrDefaultAsync(m => m.Id == medicationId && m.PatientId == patient.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Medication record was not found.");

            medication.IsActive = false;
            medication.DiscontinuedDate = DateOnly.FromDateTime(DateTime.UtcNow);
            medication.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "patient_medication.discontinued", nameof(PatientMedication), medication.Id,
                organization.Id, patientId: patient.Id, ct: HttpContext.RequestAborted);

            return Ok(ToDto(medication));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private static PatientMedicationDto ToDto(PatientMedication m) => new(
        m.Id, m.PatientId, m.Name, m.Dosage, m.Frequency, m.PrescribingProvider,
        m.StartDate, m.DiscontinuedDate, m.Notes, m.IsActive);
}

public record PatientMedicationDto(
    Guid Id, Guid PatientId, string Name, string? Dosage, string? Frequency, string? PrescribingProvider,
    DateOnly? StartDate, DateOnly? DiscontinuedDate, string? Notes, bool IsActive);

public record CreatePatientMedicationRequest(
    string Name, string? Dosage, string? Frequency, string? PrescribingProvider, DateOnly? StartDate, string? Notes);

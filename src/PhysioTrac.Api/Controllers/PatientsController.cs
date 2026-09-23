using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Patients;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patients")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public PatientsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var patients = await _tenantAccess.PatientsFor(_currentUser)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(patients.Select(ToDto));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(
                _currentUser, id,
                route: HttpContext.Request.Path,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct: HttpContext.RequestAborted);
            return Ok(ToDto(patient));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePatientRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var patient = new Patient
            {
                OrganizationId = organization.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Phone = request.Phone,
                Email = request.Email,
                AssignedTherapistId = request.AssignedTherapistId,
            };
            _db.Patients.Add(patient);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = patient.Id }, ToDto(patient));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePatientRequest request)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(
                _currentUser, id,
                route: HttpContext.Request.Path,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct: HttpContext.RequestAborted);

            patient.FirstName = request.FirstName;
            patient.LastName = request.LastName;
            patient.Phone = request.Phone;
            patient.Email = request.Email;
            patient.AssignedTherapistId = request.AssignedTherapistId;
            patient.Status = request.Status;
            patient.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(patient));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    private static PatientDto ToDto(Patient p) => new(
        p.Id, p.MedicalRecordNumber, p.FirstName, p.LastName, p.FullName,
        p.DateOfBirth, p.Age, p.Phone, p.Email, p.AssignedTherapistId, p.Status);
}

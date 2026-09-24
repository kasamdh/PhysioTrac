using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Patients;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patients")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly PhysioTracDbContext _db;

    public PatientsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IAuditService audit, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _audit = audit;
        _db = db;
    }

    /// <summary>Search is a simple contains-match against name/MRN --
    /// deliberately not full-text search infra, which this app doesn't have.
    /// SortBy is a fixed vocabulary switched in code, never a client-supplied
    /// column name fed into a dynamic OrderBy.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null, [FromQuery] PatientStatus? status = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool descending = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try
        {
            var query = _tenantAccess.PatientsFor(_currentUser);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.FirstName.Contains(term) || p.LastName.Contains(term) || p.MedicalRecordNumber.Contains(term));
            }
            if (status is not null)
            {
                query = query.Where(p => p.Status == status);
            }

            query = (sortBy?.ToLowerInvariant(), descending) switch
            {
                ("dateofbirth", false) => query.OrderBy(p => p.DateOfBirth),
                ("dateofbirth", true) => query.OrderByDescending(p => p.DateOfBirth),
                ("createdat", false) => query.OrderBy(p => p.CreatedAt),
                ("createdat", true) => query.OrderByDescending(p => p.CreatedAt),
                (_, true) => query.OrderByDescending(p => p.LastName).ThenByDescending(p => p.FirstName),
                _ => query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName),
            };

            var total = await query.CountAsync(HttpContext.RequestAborted);
            var clampedPageSize = Math.Clamp(pageSize, 1, 100);
            var clampedPage = Math.Max(page, 1);
            var patients = await query
                .Skip((clampedPage - 1) * clampedPageSize).Take(clampedPageSize)
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(new PagedPatientsDto(patients.Select(ToDto).ToList(), total, clampedPage, clampedPageSize));
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

            // "Opening a chart" is specifically this action -- Update and
            // every other RequirePatientAccessAsync caller (documents,
            // consents, messages, insurance...) has its own more specific
            // audit event where one matters; auditing "viewed" there too
            // would just be noise on top of what those already record.
            await _audit.RecordAuditEventAsync(
                _currentUser.UserId, "patient.viewed", nameof(Patient), patient.Id, patient.OrganizationId,
                patientId: patient.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct: HttpContext.RequestAborted);

            return Ok(ToDetailDto(patient));
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
                Address = request.Address,
                EmergencyContact = request.EmergencyContact,
                PreferredLanguage = request.PreferredLanguage,
                AssignedTherapistId = request.AssignedTherapistId,
                PrimaryLocationId = await ResolveOwnOrgLocationIdAsync(organization.Id, request.PrimaryLocationId, HttpContext.RequestAborted),
                PrimaryCareProviderId = await ResolveOwnOrgReferringProviderIdAsync(organization.Id, request.PrimaryCareProviderId, HttpContext.RequestAborted),
                ReferringProviderId = await ResolveOwnOrgReferringProviderIdAsync(organization.Id, request.ReferringProviderId, HttpContext.RequestAborted),
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
            patient.Address = request.Address;
            patient.EmergencyContact = request.EmergencyContact;
            patient.PreferredLanguage = request.PreferredLanguage;
            patient.AssignedTherapistId = request.AssignedTherapistId;
            patient.PrimaryLocationId = await ResolveOwnOrgLocationIdAsync(patient.OrganizationId, request.PrimaryLocationId, HttpContext.RequestAborted);
            patient.PrimaryCareProviderId = await ResolveOwnOrgReferringProviderIdAsync(patient.OrganizationId, request.PrimaryCareProviderId, HttpContext.RequestAborted);
            patient.ReferringProviderId = await ResolveOwnOrgReferringProviderIdAsync(patient.OrganizationId, request.ReferringProviderId, HttpContext.RequestAborted);
            patient.Status = request.Status;
            patient.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDetailDto(patient));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    /// <summary>Soft delete -- a chart is never hard-deleted (billing/audit
    /// history must survive it). Restricted to Scheduling the same as
    /// Create, since registering and un-registering a chart are the same
    /// front-desk responsibility.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, id, ct: HttpContext.RequestAborted);

            patient.DeletedAt = DateTimeOffset.UtcNow;
            patient.DeletedById = _currentUser.UserId;
            patient.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            // Bypasses PatientsFor deliberately -- that queryset excludes
            // soft-deleted rows by design, so restoring one has to look
            // past it. Still org-scoped by hand immediately below.
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Patient was not found.");
            if (patient.OrganizationId != organization.Id)
            {
                throw new NotFoundException("Patient was not found.");
            }

            patient.DeletedAt = null;
            patient.DeletedById = null;
            patient.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDetailDto(patient));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    /// <summary>Everything about a patient a chart view would want, grouped
    /// by module and newest-first. Modules this codebase doesn't have yet
    /// (patient-facing intake "forms") return an empty array rather than
    /// erroring or omitting the key, so a client can render one consistent
    /// empty state instead of branching on which keys exist.</summary>
    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid id)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, id, ct: HttpContext.RequestAborted);
            var ct = HttpContext.RequestAborted;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Materialize each set first, then map to the DTO in memory --
            // enum-to-string and DateOnly comparisons don't reliably
            // translate through EF Core's LINQ provider, so this avoids
            // putting either inside a Select the database has to run.
            var appointments = await _db.Appointments.Where(a => a.PatientId == patient.Id)
                .OrderByDescending(a => a.StartsAt).ToListAsync(ct);
            var notes = await _db.ClinicalNotes.Where(n => n.PatientId == patient.Id)
                .OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
            var documents = await _db.PatientDocuments.Where(d => d.PatientId == patient.Id && d.DeletedAt == null)
                .OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
            var invoices = await _db.PatientStatements.Where(s => s.PatientId == patient.Id)
                .OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
            var payments = await _db.PatientPayments.Where(p => p.PatientId == patient.Id)
                .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

            return Ok(new PatientTimelineDto(
                appointments.Select(a => new TimelineEntryDto(a.Id, "appointment", a.StartsAt, a.Status.ToString())).ToList(),
                notes.Select(n => new TimelineEntryDto(n.Id, "clinicalNote", n.CreatedAt, n.Status.ToString())).ToList(),
                documents.Select(d => new TimelineEntryDto(d.Id, "document", d.CreatedAt, d.Category.ToString())).ToList(),
                Forms: [], // No patient-facing intake-forms module exists yet.
                invoices.Select(s => new TimelineEntryDto(s.Id, "invoice", s.CreatedAt, s.DueDate < today ? "Overdue" : "Open")).ToList(),
                payments.Select(p => new TimelineEntryDto(p.Id, "payment", p.CreatedAt, p.Status.ToString())).ToList()));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    private async Task<Guid?> ResolveOwnOrgLocationIdAsync(Guid organizationId, Guid? locationId, CancellationToken ct)
    {
        if (locationId is null) return null;
        var exists = await _db.Locations.AnyAsync(l => l.Id == locationId && l.OrganizationId == organizationId, ct);
        return exists ? locationId : null;
    }

    private async Task<Guid?> ResolveOwnOrgReferringProviderIdAsync(Guid organizationId, Guid? referringProviderId, CancellationToken ct)
    {
        if (referringProviderId is null) return null;
        var exists = await _db.ReferringProviders.AnyAsync(r => r.Id == referringProviderId && r.OrganizationId == organizationId, ct);
        return exists ? referringProviderId : null;
    }

    private static PatientDto ToDto(Patient p) => new(
        p.Id, p.MedicalRecordNumber, p.FirstName, p.LastName, p.FullName,
        p.DateOfBirth, p.Age, p.Phone, p.Email, p.AssignedTherapistId, p.Status, p.PrimaryLocationId);

    private static PatientDetailDto ToDetailDto(Patient p) => new(
        p.Id, p.MedicalRecordNumber, p.FirstName, p.LastName, p.FullName, p.DateOfBirth, p.Age,
        p.Phone, p.Email, p.Address, p.EmergencyContact, p.PreferredLanguage, p.Diagnoses, p.Precautions,
        p.AssignedTherapistId, p.PrimaryLocationId, p.PrimaryCareProviderId, p.ReferringProviderId, p.Status);
}

public record TimelineEntryDto(Guid Id, string Type, DateTimeOffset OccurredAt, string Status);

public record PatientTimelineDto(
    IReadOnlyList<TimelineEntryDto> Appointments,
    IReadOnlyList<TimelineEntryDto> Notes,
    IReadOnlyList<TimelineEntryDto> Documents,
    IReadOnlyList<TimelineEntryDto> Forms,
    IReadOnlyList<TimelineEntryDto> Invoices,
    IReadOnlyList<TimelineEntryDto> Payments);

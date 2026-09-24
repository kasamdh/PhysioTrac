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

/// <summary>An organization's configuration of which CPT code represents
/// each treatment category -- see CptCodeMapping's own doc comment for why
/// this exists (it's what makes ChargeService.GenerateFromNoteAsync
/// possible). A new mapping for a category already mapped replaces the old
/// one outright (deactivates it) rather than requiring a separate "update"
/// action -- there's no versioning need here the way template engines have,
/// since a superseded mapping carries no historical meaning once no charge
/// will ever be generated against it again.</summary>
[ApiController]
[Route("api/v1/cpt-code-mappings")]
[Authorize]
public class CptCodeMappingsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public CptCodeMappingsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var mappings = await _db.CptCodeMappings.Where(m => m.OrganizationId == organization.Id && m.IsActive)
                .OrderBy(m => m.InterventionCategory).ToListAsync(HttpContext.RequestAborted);
            return Ok(mappings);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCptCodeMappingRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.CptCode))
            {
                return UnprocessableEntity(new { detail = "A CPT code is required." });
            }

            var existing = await _db.CptCodeMappings.FirstOrDefaultAsync(
                m => m.OrganizationId == organization.Id && m.InterventionCategory == request.InterventionCategory && m.IsActive,
                HttpContext.RequestAborted);
            if (existing is not null)
            {
                existing.IsActive = false;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var mapping = new CptCodeMapping
            {
                OrganizationId = organization.Id,
                InterventionCategory = request.InterventionCategory,
                CptCode = request.CptCode.Trim().ToUpperInvariant(),
                CreatedById = _currentUser.UserId,
            };
            _db.CptCodeMappings.Add(mapping);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, mapping);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record CreateCptCodeMappingRequest(InterventionCategory InterventionCategory, string CptCode);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/appointment-types")]
[Authorize]
public class AppointmentTypesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public AppointmentTypesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var types = await _db.AppointmentTypes.Where(a => a.OrganizationId == organization.Id)
                .OrderBy(a => a.Name).ToListAsync(HttpContext.RequestAborted);
            return Ok(types.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentTypeRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var type = new AppointmentType
            {
                OrganizationId = organization.Id,
                Name = request.Name,
                Description = request.Description,
                DefaultDurationMinutes = request.DefaultDurationMinutes,
                Price = request.Price,
                OnlineBookingEnabled = request.OnlineBookingEnabled,
                RequiresNewPatient = request.RequiresNewPatient,
                DefaultKind = request.DefaultKind,
                DefaultCptCode = request.DefaultCptCode,
            };
            _db.AppointmentTypes.Add(type);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(type));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/billing")]
    public async Task<IActionResult> UpdateBilling(Guid id, [FromBody] UpdateAppointmentTypeBillingRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var type = await _db.AppointmentTypes.FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organization.Id, HttpContext.RequestAborted);
            if (type is null) return NotFound(new { detail = "Appointment type was not found." });

            type.DefaultCptCode = request.DefaultCptCode;
            type.Price = request.Price;
            type.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(type));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    private static AppointmentTypeDto ToDto(AppointmentType a) => new(
        a.Id, a.Name, a.Description, a.DefaultDurationMinutes, a.Price, a.IsActive, a.OnlineBookingEnabled, a.DefaultKind, a.DefaultCptCode);
}

public record UpdateAppointmentTypeBillingRequest(string? DefaultCptCode, decimal? Price);

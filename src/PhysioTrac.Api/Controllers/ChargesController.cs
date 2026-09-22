using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/charges")]
[Authorize]
public class ChargesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IChargeService _charges;

    public ChargesController(ICurrentUser currentUser, IChargeService charges)
    {
        _currentUser = currentUser;
        _charges = charges;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var charges = await _charges.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(charges.Select(ToDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var charge = await _charges.GetAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(charge));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChargeRequest request)
    {
        try
        {
            var charge = await _charges.CreateAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = charge.Id }, ToDto(charge));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateChargeStatusRequest request)
    {
        try
        {
            var charge = await _charges.UpdateStatusAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(charge));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private static ChargeDto ToDto(Charge c) => new(
        c.Id, c.PatientId, c.ClinicalNoteId, c.ProviderId, c.LocationId, c.ServiceDate, c.CptCode,
        JsonSerializer.Deserialize<List<string>>(c.ModifiersJson) ?? new(), c.Units, c.Minutes,
        c.RecommendedUnits, c.UnitsDifference, c.UnitsOverrideReason, c.ChargeAmount, c.Status);
}

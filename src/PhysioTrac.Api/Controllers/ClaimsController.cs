using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/claims")]
[Authorize]
public class ClaimsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IClaimService _claims;

    public ClaimsController(ICurrentUser currentUser, IClaimService claims)
    {
        _currentUser = currentUser;
        _claims = claims;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var claims = await _claims.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            var dtos = new List<ClaimDto>();
            foreach (var claim in claims)
            {
                dtos.Add(await ToDtoAsync(claim));
            }
            return Ok(dtos);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var claim = await _claims.GetAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(await ToDtoAsync(claim));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClaimRequest request)
    {
        try
        {
            var claim = await _claims.CreateFromChargesAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = claim.Id }, await ToDtoAsync(claim));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateClaimStatusRequest request)
    {
        try
        {
            var claim = await _claims.UpdateStatusAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(await ToDtoAsync(claim));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task<ClaimDto> ToDtoAsync(Claim c)
    {
        var totals = await _claims.GetTotalsAsync(c.Id, _currentUser, HttpContext.RequestAborted);
        return new ClaimDto(c.Id, c.PatientId, c.PatientInsuranceId, c.PayerId, c.DiagnosisCodeList,
            c.Status, c.ClearinghouseClaimId, c.SubmittedAt, c.ClosedAt, totals);
    }
}

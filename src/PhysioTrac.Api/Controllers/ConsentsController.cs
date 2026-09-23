using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patients/{patientId:guid}/consents")]
[Authorize]
public class ConsentsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IConsentService _consents;

    public ConsentsController(ICurrentUser currentUser, IConsentService consents)
    {
        _currentUser = currentUser;
        _consents = consents;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId)
    {
        try
        {
            var consents = await _consents.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(consents.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    /// <summary>Returns the current fixed consent text for every type, so a
    /// client can render "what am I agreeing to" before POSTing a signature.</summary>
    [HttpGet("text")]
    public IActionResult GetConsentText() =>
        Ok(Enum.GetValues<ConsentType>().Select(t => new { type = t, text = ConsentTypeText.For(t) }));

    [HttpPost]
    public async Task<IActionResult> Record(Guid patientId, [FromBody] RecordConsentBody body)
    {
        try
        {
            var request = new RecordConsentRequest(patientId, body.ConsentType, body.SignedByName);
            var consent = await _consents.RecordAsync(request, _currentUser, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), new { patientId }, ToDto(consent));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPost("{consentId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid patientId, Guid consentId, [FromBody] RevokeConsentRequest request)
    {
        try
        {
            var consent = await _consents.RevokeAsync(consentId, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(consent));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static ConsentDto ToDto(Consent c) => new(
        c.Id, c.PatientId, c.ConsentType, c.SignedByName, c.RecordedById, c.SignedAt, c.IsActive, c.RevokedAt);
}

public record RecordConsentBody(ConsentType ConsentType, string SignedByName);

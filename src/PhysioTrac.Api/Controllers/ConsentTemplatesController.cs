using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>Configurable consent-language templates: definitions with
/// versioning, and resolution (Location -> State -> Organization -> Platform
/// default). See ConsentTemplate's own doc comment for the scoping rules,
/// identical to ClinicalTemplatesController's.</summary>
[ApiController]
[Route("api/v1/consent-templates")]
[Authorize]
public class ConsentTemplatesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IConsentTemplateService _templates;

    public ConsentTemplatesController(ICurrentUser currentUser, IConsentTemplateService templates)
    {
        _currentUser = currentUser;
        _templates = templates;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            var templates = await _templates.ListAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(templates.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] ConsentType consentType, [FromQuery] Guid? locationId = null, [FromQuery] string? state = null)
    {
        try
        {
            var template = await _templates.ResolveAsync(_currentUser, consentType, locationId, state, HttpContext.RequestAborted);
            return template is null ? NoContent() : Ok(ToDto(template));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConsentTemplateRequest request)
    {
        try
        {
            var template = await _templates.CreateAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(template));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static ConsentTemplateDto ToDto(ConsentTemplate t) => new(
        t.Id, t.ConsentType, t.Scope, t.State, t.LocationId, t.BodyText, t.Version, t.IsActive);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>The configurable clinical-note template engine: definitions
/// with versioning, and resolution (Location -> State -> Organization ->
/// Platform default). See ClinicalNoteTemplate's own doc comment for
/// SchemaJson's documented shape and the scoping rules.</summary>
[ApiController]
[Route("api/v1/clinical-templates")]
[Authorize]
public class ClinicalTemplatesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IClinicalTemplateService _templates;

    public ClinicalTemplatesController(ICurrentUser currentUser, IClinicalTemplateService templates)
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
    public async Task<IActionResult> Resolve([FromQuery] NoteType noteType, [FromQuery] Guid? locationId = null, [FromQuery] string? state = null)
    {
        try
        {
            var template = await _templates.ResolveAsync(_currentUser, noteType, locationId, state, HttpContext.RequestAborted);
            return template is null ? NoContent() : Ok(ToDto(template));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClinicalNoteTemplateRequest request)
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

    private static ClinicalNoteTemplateDto ToDto(ClinicalNoteTemplate t) => new(
        t.Id, t.NoteType, t.Scope, t.State, t.LocationId, t.Name, t.SchemaJson, t.Version, t.IsActive);
}

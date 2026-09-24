using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Intake;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

/// <summary>The configurable intake-form-builder engine: definitions with
/// versioning, and resolution (Location -> State -> Organization -> Platform
/// default) -- structurally identical to ClinicalTemplatesController, keyed
/// by a string Key instead of NoteType. Rendering SchemaJson into an actual
/// form is a frontend concern, deferred like every other note-taking/portal
/// screen this session.</summary>
[ApiController]
[Route("api/v1/intake-form-templates")]
[Authorize]
public class IntakeFormTemplatesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IIntakeFormService _forms;

    public IntakeFormTemplatesController(ICurrentUser currentUser, IIntakeFormService forms)
    {
        _currentUser = currentUser;
        _forms = forms;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            var templates = await _forms.ListTemplatesAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(templates.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] string key, [FromQuery] Guid? locationId = null, [FromQuery] string? state = null)
    {
        try
        {
            var template = await _forms.ResolveTemplateAsync(_currentUser, key, locationId, state, HttpContext.RequestAborted);
            return template is null ? NoContent() : Ok(ToDto(template));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIntakeFormTemplateRequest request)
    {
        try
        {
            var template = await _forms.CreateTemplateAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(template));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static IntakeFormTemplateDto ToDto(IntakeFormTemplate t) => new(
        t.Id, t.Key, t.Scope, t.State, t.LocationId, t.Name, t.SchemaJson, t.Version, t.IsActive);
}

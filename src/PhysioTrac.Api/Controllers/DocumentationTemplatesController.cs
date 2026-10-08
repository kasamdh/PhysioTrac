using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>Documentation templates: list, read (any version), create, edit
/// (publishes a new version), copy, activate/deactivate, favorites, and the
/// suggested template for a new note.</summary>
[ApiController]
[Route("api/v1/documentation-templates")]
[Authorize]
public class DocumentationTemplatesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IDocumentationTemplateService _templates;

    public DocumentationTemplatesController(ICurrentUser currentUser, IDocumentationTemplateService templates)
    {
        _currentUser = currentUser;
        _templates = templates;
    }

    [HttpGet]
    public Task<IActionResult> List([FromQuery] NoteType? noteType, [FromQuery] ClinicalSpecialty? specialty,
        [FromQuery] bool includeInactive = false, [FromQuery] string? search = null) =>
        Run(async ct => Ok(await _templates.ListAsync(_currentUser, noteType, specialty, includeInactive, search, ct)));

    [HttpGet("suggest")]
    public Task<IActionResult> Suggest([FromQuery] NoteType noteType, [FromQuery] Guid? appointmentTypeId, [FromQuery] ClinicalSpecialty? specialty) =>
        Run(async ct => await _templates.SuggestAsync(_currentUser, noteType, appointmentTypeId, specialty, ct) is { } t ? Ok(t) : NoContent());

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id) => Run(async ct => Ok(await _templates.GetAsync(id, _currentUser, ct)));

    [HttpGet("{id:guid}/versions")]
    public Task<IActionResult> Versions(Guid id) => Run(async ct => Ok(await _templates.ListVersionsAsync(id, _currentUser, ct)));

    [HttpGet("versions/{versionId:guid}")]
    public Task<IActionResult> Version(Guid versionId) => Run(async ct => Ok(await _templates.GetVersionAsync(versionId, _currentUser, ct)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] SaveDocumentationTemplateRequest request) =>
        Run(async ct =>
        {
            var created = await _templates.CreateAsync(request, _currentUser, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Template.Id }, created);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveDocumentationTemplateRequest request) =>
        Run(async ct => Ok(await _templates.UpdateAsync(id, request, _currentUser, ct)));

    [HttpPut("{id:guid}/active")]
    public Task<IActionResult> SetActive(Guid id, [FromBody] SetTemplateActiveRequest request) =>
        Run(async ct => Ok(await _templates.SetActiveAsync(id, request.IsActive, _currentUser, ct)));

    [HttpPost("{id:guid}/copy")]
    public Task<IActionResult> Copy(Guid id, [FromBody] CopyTemplateRequest? request) =>
        Run(async ct =>
        {
            var copy = await _templates.CopyAsync(id, request ?? new CopyTemplateRequest(), _currentUser, ct);
            return CreatedAtAction(nameof(Get), new { id = copy.Template.Id }, copy);
        });

    [HttpPut("{id:guid}/favorite")]
    public Task<IActionResult> Favorite(Guid id) => Run(async ct => { await _templates.SetFavoriteAsync(id, true, _currentUser, ct); return NoContent(); });

    [HttpDelete("{id:guid}/favorite")]
    public Task<IActionResult> Unfavorite(Guid id) => Run(async ct => { await _templates.SetFavoriteAsync(id, false, _currentUser, ct); return NoContent(); });

    private async Task<IActionResult> Run(Func<CancellationToken, Task<IActionResult>> action)
    {
        try
        {
            return await action(HttpContext.RequestAborted);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (TemplateValidationException ex) { return UnprocessableEntity(new { detail = ex.Message, errors = ex.Errors }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}

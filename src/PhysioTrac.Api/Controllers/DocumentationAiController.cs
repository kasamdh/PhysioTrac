using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;

namespace PhysioTrac.Api.Controllers;

/// <summary>AI-assisted drafting. Returns suggestions only; the clinician
/// inserts, edits, saves and signs the note through the normal endpoints.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class DocumentationAiController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IDocumentationAiService _ai;

    public DocumentationAiController(ICurrentUser currentUser, IDocumentationAiService ai)
    {
        _currentUser = currentUser;
        _ai = ai;
    }

    /// <summary>Whether AI drafting is on, and which provider.</summary>
    [HttpGet("documentation/ai/status")]
    public IActionResult Status() => Ok(_ai.GetStatus());

    /// <summary>Drafts the assessment or plan of a note being written.</summary>
    [HttpPost("notes/{id:guid}/ai/{section}/draft")]
    public Task<IActionResult> Draft(Guid id, string section) =>
        Run(section, async s => Ok(await _ai.DraftAsync(id, s, _currentUser, HttpContext.RequestAborted)));

    /// <summary>The clinician inserted the suggestion into the note.</summary>
    [HttpPost("notes/{id:guid}/ai/{section}/inserted")]
    public Task<IActionResult> Inserted(Guid id, string section) =>
        Run(section, async s =>
        {
            await _ai.RecordInsertedAsync(id, s, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        });

    private async Task<IActionResult> Run(string section, Func<AiDraftSection, Task<IActionResult>> action)
    {
        if (!Enum.TryParse<AiDraftSection>(section, ignoreCase: true, out var s) || !Enum.IsDefined(s))
            return UnprocessableEntity(new { detail = "AI can draft the assessment or the plan only." });
        try
        {
            return await action(s);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }
}

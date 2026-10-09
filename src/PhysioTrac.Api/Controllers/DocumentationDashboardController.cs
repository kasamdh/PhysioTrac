using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/documentation/dashboard")]
[Authorize]
public class DocumentationDashboardController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IClinicalNoteService _notes;

    public DocumentationDashboardController(ICurrentUser currentUser, IClinicalNoteService notes)
    {
        _currentUser = currentUser;
        _notes = notes;
    }

    /// <summary>Today's visits, notes by state, overdue notes and deadlines,
    /// for the patients the caller may see. All filters are optional.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? providerId, [FromQuery] Guid? patientId, [FromQuery] NoteType? noteType,
        [FromQuery] DocumentationStatus? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        try
        {
            return Ok(await _notes.GetDashboardAsync(new DashboardFilter(providerId, patientId, noteType, status, from, to),
                _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Messaging;

namespace PhysioTrac.Api.Controllers;

/// <summary>Cross-patient messaging inbox. Reading and sending within one
/// thread stays on MessagesController (/patients/{id}/messages).</summary>
[ApiController]
[Route("api/v1/messages/threads")]
[Authorize]
public class MessageThreadsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessageService _messages;

    public MessageThreadsController(ICurrentUser currentUser, IMessageService messages)
    {
        _currentUser = currentUser;
        _messages = messages;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            return Ok(await _messages.ListThreadsAsync(_currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Messaging;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patients/{patientId:guid}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessageService _messages;

    public MessagesController(ICurrentUser currentUser, IMessageService messages)
    {
        _currentUser = currentUser;
        _messages = messages;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId)
    {
        try
        {
            var messages = await _messages.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(messages.Select(ToDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Send(Guid patientId, [FromBody] SendMessageBody body)
    {
        try
        {
            var message = await _messages.SendAsync(new SendMessageRequest(patientId, body.Body), _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), new { patientId }, ToDto(message));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead(Guid patientId)
    {
        try
        {
            await _messages.MarkThreadReadAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    private static MessageDto ToDto(Message m) => new(
        m.Id, m.PatientId, m.SenderId, m.SenderRole, m.IsFromPatient, m.Body, m.SentAt, m.ReadAt);
}

public record SendMessageBody(string Body);

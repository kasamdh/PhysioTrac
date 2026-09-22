using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/outcomes")]
[Authorize]
public class OutcomesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IOutcomeScoreService _outcomes;

    public OutcomesController(ICurrentUser currentUser, IOutcomeScoreService outcomes)
    {
        _currentUser = currentUser;
        _outcomes = outcomes;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var scores = await _outcomes.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(scores.Select(ToDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordOutcomeScoreRequest request)
    {
        try
        {
            var score = await _outcomes.RecordAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForPatient), new { patientId = score.PatientId }, ToDto(score));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static OutcomeScoreDto ToDto(OutcomeScore o) => new(o.Id, o.PatientId, o.NoteId, o.RecordedById, o.Measure, o.MeasuredOn, o.Score, o.MaximumScore);
}

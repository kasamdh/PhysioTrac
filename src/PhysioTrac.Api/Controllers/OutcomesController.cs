using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;

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

    /// <summary>The measures that can be recorded: items, response options,
    /// scoring method, interpretation bands and meaningful-change values.</summary>
    [HttpGet("measures")]
    public IActionResult Measures() => Ok(OutcomeMeasureCatalog.All);

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            return Ok(await _outcomes.ListDtosForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordOutcomeScoreRequest request)
    {
        try
        {
            var score = await _outcomes.RecordAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForPatient), new { patientId = score.PatientId }, OutcomeScoreMapper.ToDto(score, false));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _outcomes.DeleteAsync(id, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}

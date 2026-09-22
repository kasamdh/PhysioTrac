using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/goals")]
[Authorize]
public class GoalsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IFunctionalGoalService _goals;

    public GoalsController(ICurrentUser currentUser, IFunctionalGoalService goals)
    {
        _currentUser = currentUser;
        _goals = goals;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var goals = await _goals.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(goals.Select(ToDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoalRequest request)
    {
        try
        {
            var goal = await _goals.CreateAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForPatient), new { patientId = goal.PatientId }, ToDto(goal));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        try
        {
            var goal = await _goals.ApproveAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(goal));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(Guid id, [FromBody] UpdateGoalProgressRequest request)
    {
        try
        {
            var goal = await _goals.UpdateProgressAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(goal));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private static FunctionalGoalDto ToDto(FunctionalGoal g) => new(
        g.Id, g.PatientId, g.AuthorId, g.FunctionalLimitation, g.FunctionalTask,
        g.BaselineValue, g.TargetValue, g.CurrentValue, g.Unit, g.MeasurementMethod,
        g.TargetDate, g.Status, g.ProgressPercent, g.ApprovedById, g.ApprovedAt);
}

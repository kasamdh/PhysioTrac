using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patients/{patientId:guid}/home-exercise-programs")]
[Authorize]
public class HomeExerciseProgramsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IHomeExerciseProgramService _programs;

    public HomeExerciseProgramsController(ICurrentUser currentUser, IHomeExerciseProgramService programs)
    {
        _currentUser = currentUser;
        _programs = programs;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId)
    {
        try
        {
            var programs = await _programs.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(programs.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] CreateHomeExerciseProgramRequest request)
    {
        try
        {
            var program = await _programs.CreateAsync(request with { PatientId = patientId }, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), new { patientId }, ToDto(program));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPost("{programId:guid}/items")]
    public async Task<IActionResult> AddItem(Guid patientId, Guid programId, [FromBody] CreateHomeExerciseItemRequest request)
    {
        try
        {
            var item = await _programs.AddItemAsync(programId, request, _currentUser, HttpContext.RequestAborted);
            var dto = new HomeExerciseItemDto(
                item.Id, item.Name, item.Description, item.Sets, item.Reps, item.HoldSeconds, item.FrequencyPerDay, item.Notes, item.MediaUrl, item.Order);
            return CreatedAtAction(nameof(List), new { patientId }, dto);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpDelete("{programId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid patientId, Guid programId, Guid itemId)
    {
        try
        {
            await _programs.RemoveItemAsync(programId, itemId, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("{programId:guid}/discontinue")]
    public async Task<IActionResult> Discontinue(Guid patientId, Guid programId)
    {
        try
        {
            var program = await _programs.DiscontinueAsync(programId, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(program));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static HomeExerciseProgramDto ToDto(HomeExerciseProgram p) => new(
        p.Id, p.PatientId, p.Title, p.GeneralInstructions, p.Status, p.CreatedById, p.CreatedAt,
        p.Items.OrderBy(i => i.Order).Select(i => new HomeExerciseItemDto(
            i.Id, i.Name, i.Description, i.Sets, i.Reps, i.HoldSeconds, i.FrequencyPerDay, i.Notes, i.MediaUrl, i.Order)).ToList());
}

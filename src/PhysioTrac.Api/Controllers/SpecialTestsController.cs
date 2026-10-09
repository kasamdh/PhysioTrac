using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>The special-test library: search with favorites, and management
/// (add, edit, activate/deactivate) for administrators.</summary>
[ApiController]
[Route("api/v1/special-tests")]
[Authorize]
public class SpecialTestsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly ISpecialTestLibraryService _library;

    public SpecialTestsController(ICurrentUser currentUser, ISpecialTestLibraryService library)
    {
        _currentUser = currentUser;
        _library = library;
    }

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] string? search, [FromQuery] ClinicalSpecialty? specialty,
        [FromQuery] bool includeInactive = false, [FromQuery] bool favoritesOnly = false) =>
        Run(async ct => Ok(await _library.SearchAsync(_currentUser, search, specialty, includeInactive, favoritesOnly, ct)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] SaveSpecialTestDefinitionRequest request) =>
        Run(async ct => StatusCode(201, await _library.CreateAsync(request, _currentUser, ct)));

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveSpecialTestDefinitionRequest request) =>
        Run(async ct => Ok(await _library.UpdateAsync(id, request, _currentUser, ct)));

    [HttpPut("{id:guid}/active")]
    public Task<IActionResult> SetActive(Guid id, [FromBody] SetTemplateActiveRequest request) =>
        Run(async ct => Ok(await _library.SetActiveAsync(id, request.IsActive, _currentUser, ct)));

    [HttpPut("{id:guid}/favorite")]
    public Task<IActionResult> Favorite(Guid id) => Run(async ct => { await _library.SetFavoriteAsync(id, true, _currentUser, ct); return NoContent(); });

    [HttpDelete("{id:guid}/favorite")]
    public Task<IActionResult> Unfavorite(Guid id) => Run(async ct => { await _library.SetFavoriteAsync(id, false, _currentUser, ct); return NoContent(); });

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

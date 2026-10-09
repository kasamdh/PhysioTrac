using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>The approved intervention library, reusable intervention groups,
/// and favorites of both.</summary>
[ApiController]
[Route("api/v1/intervention-library")]
[Authorize]
public class InterventionLibraryController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IInterventionLibraryService _library;

    public InterventionLibraryController(ICurrentUser currentUser, IInterventionLibraryService library)
    {
        _currentUser = currentUser;
        _library = library;
    }

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] string? search, [FromQuery] InterventionCategory? category,
        [FromQuery] bool favoritesOnly = false, [FromQuery] bool includeInactive = false) =>
        Run(async ct => Ok(await _library.SearchAsync(_currentUser, search, category, favoritesOnly, includeInactive, ct)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] SaveInterventionLibraryItemRequest request) =>
        Run(async ct => StatusCode(201, await _library.CreateItemAsync(request, _currentUser, ct)));

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveInterventionLibraryItemRequest request) =>
        Run(async ct => Ok(await _library.UpdateItemAsync(id, request, _currentUser, ct)));

    [HttpPut("{id:guid}/active")]
    public Task<IActionResult> SetActive(Guid id, [FromBody] SetTemplateActiveRequest request) =>
        Run(async ct => Ok(await _library.SetItemActiveAsync(id, request.IsActive, _currentUser, ct)));

    [HttpPut("{id:guid}/favorite")]
    public Task<IActionResult> Favorite(Guid id) => Run(async ct => { await _library.SetItemFavoriteAsync(id, true, _currentUser, ct); return NoContent(); });

    [HttpDelete("{id:guid}/favorite")]
    public Task<IActionResult> Unfavorite(Guid id) => Run(async ct => { await _library.SetItemFavoriteAsync(id, false, _currentUser, ct); return NoContent(); });

    [HttpGet("groups")]
    public Task<IActionResult> Groups() => Run(async ct => Ok(await _library.ListGroupsAsync(_currentUser, ct)));

    [HttpPost("groups")]
    public Task<IActionResult> CreateGroup([FromBody] SaveInterventionGroupRequest request) =>
        Run(async ct => StatusCode(201, await _library.CreateGroupAsync(request, _currentUser, ct)));

    [HttpPut("groups/{id:guid}")]
    public Task<IActionResult> UpdateGroup(Guid id, [FromBody] SaveInterventionGroupRequest request) =>
        Run(async ct => Ok(await _library.UpdateGroupAsync(id, request, _currentUser, ct)));

    [HttpDelete("groups/{id:guid}")]
    public Task<IActionResult> DeleteGroup(Guid id) => Run(async ct => { await _library.DeleteGroupAsync(id, _currentUser, ct); return NoContent(); });

    [HttpPut("groups/{id:guid}/favorite")]
    public Task<IActionResult> FavoriteGroup(Guid id) => Run(async ct => { await _library.SetGroupFavoriteAsync(id, true, _currentUser, ct); return NoContent(); });

    [HttpDelete("groups/{id:guid}/favorite")]
    public Task<IActionResult> UnfavoriteGroup(Guid id) => Run(async ct => { await _library.SetGroupFavoriteAsync(id, false, _currentUser, ct); return NoContent(); });

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

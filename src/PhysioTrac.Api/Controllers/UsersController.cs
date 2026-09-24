using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Users;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>Org-scoped staff account administration (list/invite/role/
/// activate/deactivate) -- OrganizationAdministration-only (Admin/Director),
/// enforced inside IUserManagementService itself. Distinct from
/// SuperAdminClientsController, which provisions a brand-new organization
/// and its first admin and is platform-super-admin-only.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserManagementService _users;

    public UsersController(ICurrentUser currentUser, IUserManagementService users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            var users = await _users.ListAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(users);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request)
    {
        try
        {
            var (user, _, activationUrl) = await _users.InviteAsync(_currentUser, request, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, new { user, activationUrl });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeUserRoleRequest request)
    {
        try
        {
            var user = await _users.ChangeRoleAsync(_currentUser, id, request.Role, HttpContext.RequestAborted);
            return Ok(user);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateUserRequest? request)
    {
        try
        {
            var user = await _users.DeactivateAsync(_currentUser, id, request?.Reason, HttpContext.RequestAborted);
            return Ok(user);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        try
        {
            var user = await _users.ActivateAsync(_currentUser, id, HttpContext.RequestAborted);
            return Ok(user);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }
}

public record ChangeUserRoleRequest(UserRole Role);
public record DeactivateUserRequest(string? Reason);

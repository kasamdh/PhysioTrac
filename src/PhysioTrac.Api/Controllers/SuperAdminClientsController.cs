using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.SuperAdmin;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/super-admin/clients")]
[Authorize]
public class SuperAdminClientsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProvisioningService _clients;

    public SuperAdminClientsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IClientProvisioningService clients)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _clients = clients;
    }

    /// <summary>Every action here is platform-super-admin-only — same guard
    /// the original calls at the top of every `super_admin.py` view.</summary>
    private void RequireSuperAdmin() => _tenantAccess.RequirePlatformSuperAdmin(_currentUser);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] OrganizationStatus? status,
        [FromQuery(Name = "includeArchived")] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery(Name = "pageSize")] int pageSize = 25)
    {
        try
        {
            RequireSuperAdmin();
            var result = await _clients.ListClientsAsync(new ClientListQuery(query, status, includeArchived, page, pageSize), HttpContext.RequestAborted);
            return Ok(new { clients = result.Items, total = result.Total, page = result.Page, pageSize = result.PageSize });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Provision([FromBody] ProvisionClientRequest request)
    {
        try
        {
            RequireSuperAdmin();
            var result = await _clients.ProvisionClientAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { clientNumber = result.Client.ClientNumber }, new
            {
                client = result.Client,
                administrator = new { id = result.AdministratorId, email = result.AdministratorEmail },
                developmentInviteToken = result.DevelopmentInviteToken,
                invitationUrl = result.InvitationUrl,
            });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpGet("{clientNumber:long}")]
    public async Task<IActionResult> Get(long clientNumber)
    {
        try
        {
            RequireSuperAdmin();
            var client = await _clients.GetClientAsync(clientNumber, HttpContext.RequestAborted);
            return client is null ? NotFound(new { detail = "Client was not found." }) : Ok(new { client });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPatch("{clientNumber:long}")]
    public async Task<IActionResult> Update(long clientNumber, [FromBody] UpdateClientRequest request)
    {
        try
        {
            RequireSuperAdmin();
            var client = await _clients.UpdateClientAsync(clientNumber, request, _currentUser, HttpContext.RequestAborted);
            return Ok(new { client });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpDelete("{clientNumber:long}")]
    public async Task<IActionResult> Archive(long clientNumber, [FromBody] ArchiveClientRequest? request)
    {
        try
        {
            RequireSuperAdmin();
            var client = await _clients.ArchiveClientAsync(clientNumber, request?.Reason, _currentUser, HttpContext.RequestAborted);
            return Ok(new { client });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{clientNumber:long}/suspend")]
    public async Task<IActionResult> Suspend(long clientNumber, [FromBody] SuspendClientRequest request)
    {
        try
        {
            RequireSuperAdmin();
            var client = await _clients.SuspendClientAsync(clientNumber, request.Reason, _currentUser, HttpContext.RequestAborted);
            return Ok(new { client });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{clientNumber:long}/activate")]
    public async Task<IActionResult> Activate(long clientNumber)
    {
        try
        {
            RequireSuperAdmin();
            var client = await _clients.ActivateClientAsync(clientNumber, _currentUser, HttpContext.RequestAborted);
            return Ok(new { client });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("{clientNumber:long}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(long clientNumber)
    {
        try
        {
            RequireSuperAdmin();
            var (_, activationUrl) = await _clients.ResendAdminInvitationAsync(clientNumber, HttpContext.RequestAborted);
            return Ok(new { detail = "A new invitation was generated.", invitationUrl = activationUrl });
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }
}

public record ArchiveClientRequest(string? Reason);
public record SuspendClientRequest(string Reason);

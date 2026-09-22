using Microsoft.AspNetCore.Http;
using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary>Reads <see cref="ICurrentUser"/> from the authenticated
/// principal — Application/Infrastructure services depend only on the
/// interface, never on a specific host. Prefers <see cref="CurrentUserAccessor"/>
/// (set explicitly by Blazor Server's root component) and falls back to
/// <see cref="IHttpContextAccessor"/> (reliable per-request in the JSON API),
/// so the same class serves both hosts correctly.</summary>
public class ClaimsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserAccessor _currentUserAccessor;

    public ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor, CurrentUserAccessor currentUserAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserAccessor = currentUserAccessor;
    }

    private System.Security.Claims.ClaimsPrincipal? Principal =>
        _currentUserAccessor.Principal ?? _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId => Guid.TryParse(
        Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    public Guid? OrganizationId => Guid.TryParse(
        Principal?.FindFirst(AppClaimTypes.OrganizationId)?.Value, out var orgId) ? orgId : null;

    public UserRole Role => Enum.TryParse<UserRole>(
        Principal?.FindFirst(AppClaimTypes.Role)?.Value, out var role) ? role : UserRole.Patient;

    public bool IsPlatformSuperAdmin => bool.TryParse(
        Principal?.FindFirst(AppClaimTypes.IsPlatformSuperAdmin)?.Value, out var flag) && flag;
}

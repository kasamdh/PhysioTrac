using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Auth;

/// <summary>The authenticated caller, as seen by Application-layer services.
/// Implemented in the API layer from the ASP.NET Core auth cookie's claims —
/// Application code never touches HttpContext directly.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid? OrganizationId { get; }
    UserRole Role { get; }
    bool IsPlatformSuperAdmin { get; }
}

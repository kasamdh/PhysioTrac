using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Contracts;

public record LoginRequest(string Username, string Password);

public record ActivateInvitationRequest(string Token, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record MeResponse(
    Guid Id,
    string Username,
    string? Email,
    UserRole Role,
    Guid? OrganizationId,
    bool IsPlatformSuperAdmin,
    bool MustChangePassword,
    bool AccessControlEnabled,
    string FirstName,
    string LastName,
    DateTimeOffset? LastLoginAt);

public record UserSessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    string? DeviceName,
    string? BrowserName,
    string? IpAddress,
    bool IsCurrent);

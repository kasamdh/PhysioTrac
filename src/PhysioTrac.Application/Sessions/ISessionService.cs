using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Sessions;

/// <summary>Per-device session tracking layered on top of the ASP.NET Core
/// auth cookie — mirrors the original `care/session_management.py`: role-
/// based concurrency limits (oldest session revoked first when exceeded),
/// idle/absolute timeout enforcement.
///
/// Documented, carried-over gap: a session created through any path other
/// than <see cref="CreateSessionAsync"/> (e.g. a future SSO flow) is
/// untracked and fails open, exactly as in the source system.</summary>
public interface ISessionService
{
    /// <summary>Creates a new tracked session and revokes the oldest active
    /// session(s) for this user/role if the per-role concurrency limit would
    /// otherwise be exceeded. Returns the session key to store in the auth
    /// cookie's claims.</summary>
    Task<UserSession> CreateSessionAsync(
        Guid userId, Guid? organizationId, UserRole role,
        string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>Validates the session behind <paramref name="sessionKey"/>
    /// against idle/absolute timeout, touches <c>LastActivityAt</c> on
    /// success, or revokes and returns null on failure. Null return means
    /// "the caller must be signed out" — the API layer maps that to 401.</summary>
    Task<UserSession?> ValidateAndTouchAsync(string sessionKey, CancellationToken ct = default);

    Task RevokeAsync(string sessionKey, SessionRevokedReason reason, CancellationToken ct = default);

    Task<IReadOnlyList<UserSession>> ListActiveForUserAsync(Guid userId, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, SessionRevokedReason reason, string? exceptSessionKey = null, CancellationToken ct = default);
}

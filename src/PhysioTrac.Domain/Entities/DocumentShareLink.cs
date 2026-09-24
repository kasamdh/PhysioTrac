using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>A time-limited, unauthenticated access grant to one
/// <see cref="PatientDocument"/> -- for handing a document to someone
/// outside the app entirely (a referring physician, an attorney, the
/// patient themselves on a personal device) without giving them a portal
/// login. Only <see cref="TokenHash"/> is ever persisted, never the raw
/// token -- same scheme as ClientInvitation/staff-invitation tokens (see
/// InvitationTokenGenerator) -- so a database read alone can never produce
/// a working share link. Access is otherwise completely unauthenticated
/// (see SharedDocumentsController, which deliberately carries no
/// [Authorize]); the token itself, checked against ExpiresAt and
/// RevokedAt, is the entire access control.</summary>
public class DocumentShareLink : BaseEntity
{
    public Guid PatientDocumentId { get; set; }
    public PatientDocument? PatientDocument { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedById { get; set; }

    public int AccessCount { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }

    public bool IsUsable => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}

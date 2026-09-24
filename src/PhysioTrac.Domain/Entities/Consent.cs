using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One signed consent record. A patient signing the same
/// <see cref="ConsentType"/> again (e.g. an annual financial-policy
/// re-acknowledgment) creates a new row rather than overwriting the old one
/// -- <see cref="ConsentText"/> snapshots exactly what the signer agreed to,
/// so an old signature stays provable even after the clinic's consent
/// language changes.</summary>
public class Consent : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public ConsentType ConsentType { get; set; }

    /// <summary>The exact language shown to and agreed to by the signer,
    /// snapshotted at signing time -- never re-read from a live template.</summary>
    public string ConsentText { get; set; } = string.Empty;

    /// <summary>Which <see cref="ConsentTemplate"/> version ConsentText was
    /// copied from. Null when no ConsentTemplate had been configured yet at
    /// signing time and ConsentService fell back to the static
    /// ConsentTypeText content instead (see ConsentTemplate's own doc
    /// comment).</summary>
    public int? TemplateVersion { get; set; }

    /// <summary>Typed full name serving as the e-signature. Real handwritten-
    /// signature capture (a canvas/drawn signature) isn't built yet --
    /// this is a typed-name attestation, the same pattern
    /// ClinicalNote.SignatureName already uses for note signing.</summary>
    public string SignedByName { get; set; } = string.Empty;

    public Guid RecordedById { get; set; }
    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? IpAddress { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedById { get; set; }
    public string? RevocationReason { get; set; }

    public bool IsActive => RevokedAt is null;
}

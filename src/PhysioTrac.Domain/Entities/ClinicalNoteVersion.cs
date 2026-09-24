using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Append-only snapshot of a note's editable content, written on
/// every draft save (autosave-friendly -- each PUT gets its own row) and
/// once more at signing (IsSignedVersion = true, the final immutable
/// snapshot). Never updated or deleted once written -- enforced the same
/// way AuditEvent's append-only rule is, in PhysioTracDbContext. This is
/// what "immutable version history" actually means here: not a second
/// enforcement of "can't edit a signed note" (EnforceSignedNoteImmutability
/// already does that), but a real, queryable record of what the note
/// looked like at each save, including every draft revision before it was
/// ever signed.</summary>
public class ClinicalNoteVersion : BaseEntity
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public int VersionNumber { get; set; }

    /// <summary>Snapshot of every clinically-relevant field on the note at
    /// save time, serialized as JSON -- never partially reconstructed from
    /// individual columns, so a version always reflects exactly what was
    /// saved.</summary>
    public string ContentJson { get; set; } = "{}";

    public Guid SavedById { get; set; }
    public bool IsSignedVersion { get; set; }
}

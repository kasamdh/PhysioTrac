using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Metadata for a securely uploaded patient file. The file bytes
/// themselves never sit under a web-servable path (wwwroot) -- only
/// IFileStorage (Infrastructure) knows the real storage location, keyed by
/// <see cref="StorageKey"/>, and every read goes through
/// DocumentService.DownloadAsync's own tenant/patient access check, never a
/// direct static-file URL.
///
/// Deliberately never hard-deleted (see <see cref="DeletedAt"/>) -- a
/// clinician removing a mis-uploaded document from view is still an event
/// worth being able to reconstruct later, matching this app's audit-first
/// posture for anything PHI-adjacent.</summary>
public class PatientDocument : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid UploadedById { get; set; }

    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public string OriginalFilename { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }

    /// <summary>Opaque key IFileStorage uses to locate the actual bytes --
    /// never the original filename, so a hostile filename can't be used for
    /// path traversal and two patients' same-named uploads never collide.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}

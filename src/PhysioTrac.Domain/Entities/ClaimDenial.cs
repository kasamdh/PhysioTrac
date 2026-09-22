using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One denial work-queue item for a claim. A claim can be denied
/// more than once across resubmission/appeal cycles, so this is a child
/// table, not a field on <see cref="Claim"/>.</summary>
public class ClaimDenial : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public string? DenialCode { get; set; }
    public string DenialReason { get; set; } = string.Empty;
    public DateOnly DeniedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public Guid? OwnerId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? ActionNotes { get; set; }
    public AppealStatus AppealStatus { get; set; } = AppealStatus.NotAppealed;
    public DenialResolution Resolution { get; set; } = DenialResolution.Open;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? CreatedById { get; set; }

    public bool IsOverdue => Resolution == DenialResolution.Open && DueDate is DateOnly due && due < DateOnly.FromDateTime(DateTime.UtcNow);
}

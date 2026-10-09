using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>A note's documentation status as people talk about it. Stored
/// statuses (<see cref="NoteStatus"/>) plus states derived from them: Not
/// started (a visit with no note yet), Ready to sign (a draft with nothing
/// blocking its signature), Cosign required (submitted by an assistant) and
/// Cosigned (signed with a supervising PT's cosignature).</summary>
public enum DocumentationStatus
{
    NotStarted,
    Draft,
    InReview,
    ReturnedForCorrection,
    ReadyToSign,
    Signed,
    CosignRequired,
    Cosigned,
    Amended,
    Locked,
    Voided,
}

public static class LifecycleRules
{
    /// <summary>The documentation status of a note. <paramref name="readyToSign"/>
    /// is whether its signing checks pass (only meaningful for an editable note).</summary>
    public static DocumentationStatus For(NoteStatus status, bool cosigned, bool readyToSign = false) => status switch
    {
        NoteStatus.Draft => readyToSign ? DocumentationStatus.ReadyToSign : DocumentationStatus.Draft,
        NoteStatus.ReturnedForCorrection => readyToSign ? DocumentationStatus.ReadyToSign : DocumentationStatus.ReturnedForCorrection,
        NoteStatus.ReviewRequired => DocumentationStatus.CosignRequired,
        NoteStatus.InReview => DocumentationStatus.InReview,
        NoteStatus.Signed => cosigned ? DocumentationStatus.Cosigned : DocumentationStatus.Signed,
        NoteStatus.Amended => DocumentationStatus.Amended,
        NoteStatus.Locked => DocumentationStatus.Locked,
        NoteStatus.Voided => DocumentationStatus.Voided,
        _ => DocumentationStatus.Draft,
    };

    /// <summary>Note types a PT must sign: an assistant may draft them, but
    /// they always go to a supervising PT for cosignature.</summary>
    public static bool IsPtOnly(NoteType type) =>
        type is NoteType.Evaluation or NoteType.PelvicHealthEvaluation or NoteType.ReEvaluation or NoteType.Recertification
            or NoteType.Progress or NoteType.Discharge or NoteType.PlanOfCare;

    /// <summary>Why an assistant's note needs a PT's cosignature, or null
    /// when it doesn't (or the author isn't an assistant).</summary>
    public static string? CosignReason(UserRole authorRole, NoteType type, bool organizationPolicy) =>
        authorRole != UserRole.Assistant ? null
        : IsPtOnly(type) ? "note_type"
        : organizationPolicy ? "organization_policy"
        : null;

    /// <summary>Submitted and waiting for, or under, a PT's review.</summary>
    public static bool IsAwaitingReview(NoteStatus status) => status is NoteStatus.ReviewRequired or NoteStatus.InReview;

    public const int MaxReasonLength = 1000;
}

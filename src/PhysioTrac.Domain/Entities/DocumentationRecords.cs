using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Assigns a template to an appointment type: opening that kind
/// of visit suggests this template.</summary>
public class ClinicalNoteTemplateAppointmentType : BaseEntity
{
    public Guid TemplateId { get; set; }
    public ClinicalNoteTemplate? Template { get; set; }

    public Guid AppointmentTypeId { get; set; }
    public AppointmentType? AppointmentType { get; set; }
}

/// <summary>A clinician's favorite library item (template, special test,
/// intervention...). ItemId points into the table ItemType names.</summary>
public class ProviderFavorite : BaseEntity
{
    public Guid UserId { get; set; }
    public FavoriteItemType ItemType { get; set; }
    public Guid ItemId { get; set; }
}

/// <summary>The value of one template field on one note. Exactly one Value*
/// column is used, chosen by the field's type (see TemplateFieldType);
/// ValueJson only for Multiselect / StructuredTable. FieldKey is copied from
/// the field so the value stays readable with the note's own template
/// version.</summary>
public class ClinicalNoteFieldValue : BaseEntity, INoteOwned
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid FieldId { get; set; }
    public ClinicalNoteTemplateField? Field { get; set; }

    public string FieldKey { get; set; } = string.Empty;

    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateOnly? ValueDate { get; set; }
    public TimeOnly? ValueTime { get; set; }
    public bool? ValueBool { get; set; }
    public string? ValueJson { get; set; }
}

/// <summary>One status transition of a note (append-only): who, when, from
/// what to what, and why (return, void and amendment reasons).</summary>
public class ClinicalNoteStatusChange : BaseEntity
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public NoteStatus? FromStatus { get; set; }
    public NoteStatus ToStatus { get; set; }
    public Guid ChangedById { get; set; }
    public string? Reason { get; set; }
}

/// <summary>An electronic signature on a note (append-only). Captures the
/// signer's name, credentials and role as they were at signing, what the
/// signature means, the UTC time plus the timezone it was displayed in,
/// and which saved version of the note was signed.</summary>
public class ElectronicSignature : BaseEntity
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid SignerUserId { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string? Credentials { get; set; }

    /// <summary>The signer's application role at signing (e.g. "Therapist").</summary>
    public string Role { get; set; } = string.Empty;
    public SignatureMeaning Meaning { get; set; }

    /// <summary>UTC.</summary>
    public DateTimeOffset SignedAt { get; set; }

    /// <summary>IANA timezone the signer saw (e.g. "America/New_York").</summary>
    public string DisplayTimeZone { get; set; } = "UTC";

    /// <summary>ClinicalNoteVersion.VersionNumber of the content signed.</summary>
    public int NoteVersionNumber { get; set; }

    /// <summary>SHA-256 of the signed content (same as ClinicalNote.SignatureHash for the author signature).</summary>
    public string? ContentHash { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>How identity was confirmed, e.g. "password".</summary>
    public string Method { get; set; } = "password";
}

/// <summary>An assistant's note submitted for a supervising PT's cosignature.</summary>
public class NoteCosignRequest : BaseEntity
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid RequestedById { get; set; }

    /// <summary>The supervising PT's user, when one is assigned.</summary>
    public Guid? SupervisorUserId { get; set; }

    public CosignRequestStatus Status { get; set; } = CosignRequestStatus.Pending;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }

    /// <summary>Required when the PT returns the note for correction.</summary>
    public string? ResolutionComment { get; set; }
}

/// <summary>A patient document (upload) attached to a note. The file itself
/// stays in PatientDocument; attaching never copies it.</summary>
public class NoteAttachment : BaseEntity, INoteOwned, IUserStamped
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid PatientDocumentId { get; set; }
    public PatientDocument? PatientDocument { get; set; }

    public string? Caption { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>A patient's plan of care, created from a signed evaluation (or
/// a new version from a re-evaluation/recertification, keeping the previous
/// plan as Superseded) and closed by a discharge summary.</summary>
public class PlanOfCare : BaseEntity, IUserStamped
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>The note (evaluation / re-evaluation / recertification) it came from.</summary>
    public Guid SourceNoteId { get; set; }
    public ClinicalNote? SourceNote { get; set; }

    /// <summary>The plan this one replaced, if any.</summary>
    public Guid? PreviousPlanOfCareId { get; set; }
    public PlanOfCare? PreviousPlanOfCare { get; set; }

    public PlanOfCareStatus Status { get; set; } = PlanOfCareStatus.Draft;

    /// <summary>Certification period.</summary>
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public int? FrequencyPerWeek { get; set; }
    public int? DurationWeeks { get; set; }

    public string? TreatmentDiagnosis { get; set; }
    public string? Prognosis { get; set; }
    public string? RehabPotential { get; set; }
    public string? PlannedInterventions { get; set; }
    public string? HomeProgram { get; set; }
    public string? PatientEducation { get; set; }
    public string? Referrals { get; set; }

    public DateOnly? CertifiedDate { get; set; }
    public Guid? CertifyingProviderId { get; set; }
    public ReferringProvider? CertifyingProvider { get; set; }

    /// <summary>Set when a signed discharge summary closes the plan.</summary>
    public DischargeReason? DischargeReason { get; set; }
    public Guid? DischargeNoteId { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

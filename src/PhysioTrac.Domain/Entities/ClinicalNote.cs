using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Editable draft note that becomes immutable after therapist
/// signature. Deliberately omits the original's `episode_of_care`/
/// `authorization` FKs — not ported yet.</summary>
public class ClinicalNote : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Stored by id only — Domain doesn't reference Infrastructure's ApplicationUser.</summary>
    public Guid TherapistId { get; set; }

    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public NoteType NoteType { get; set; } = NoteType.Daily;
    public NoteStatus Status { get; set; } = NoteStatus.Draft;
    public DateOnly ServiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string? DiagnosisSnapshot { get; set; }
    public string? PrecautionsSnapshot { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Interventions { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }

    public DateOnly? PlanOfCareStart { get; set; }
    public DateOnly? PlanOfCareEnd { get; set; }
    public int? FrequencyPerWeek { get; set; }
    public int? DurationWeeks { get; set; }
    public DateOnly? ReassessmentDue { get; set; }

    /// <summary>The Medicare-style physician certification of this note's
    /// plan of care -- deliberately separate from the therapist's own
    /// SignedAt/SignatureName, since real-world certification is a distinct
    /// step (often days later, by an outside physician, not the treating
    /// therapist). CertifyPlanOfCareAsync is one of the few writes allowed
    /// on an already-Signed/Locked note -- see EnforceSignedNoteImmutability,
    /// which permits only these two fields (plus UpdatedAt) to change post-
    /// signature, never the clinical narrative itself.</summary>
    public DateOnly? PlanOfCareCertifiedDate { get; set; }
    public Guid? PlanOfCareCertifyingProviderId { get; set; }
    public ReferringProvider? PlanOfCareCertifyingProvider { get; set; }

    public string? SignatureName { get; set; }

    /// <summary>Snapshot of the signer's own Credential field (e.g. "PT, DPT")
    /// at the moment of signing -- never re-read live from ApplicationUser
    /// later, the same reasoning CosignRequired already snapshots the org
    /// policy at note-creation time.</summary>
    public string? SignatureCredentials { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
    public string? SignatureIpAddress { get; set; }

    /// <summary>SHA-256 hex digest of the note's clinical content at the
    /// moment of signing (see ClinicalNoteService.ComputeContentHash) --
    /// belt-and-suspenders integrity proof alongside the DB-level immutability
    /// enforcement (EnforceSignedNoteImmutability): recomputing this hash
    /// against the current row later and comparing detects any tampering
    /// that somehow bypassed that enforcement (e.g. a direct DB edit outside
    /// the app).</summary>
    public string? SignatureHash { get; set; }

    public bool FinalizationAttestation { get; set; }

    /// <summary>Structured section blobs (ROM/MMT/special tests, discharge
    /// specifics, home-visit context) — one JSON string per note, never
    /// queried across notes in SQL, matching the original's JSONField precedent.</summary>
    public string SubjectiveDetailsJson { get; set; } = "{}";
    public string ObjectiveMeasurementsJson { get; set; } = "{}";
    public string DischargeDetailsJson { get; set; } = "{}";
    public string HomeVisitDetailsJson { get; set; } = "{}";

    /// <summary>Snapshotted from <see cref="Organization.PtaCosignRequired"/>
    /// at creation time so a later org-policy change never silently changes
    /// an in-progress note's requirement.</summary>
    public bool CosignRequired { get; set; }
    public Guid? CosignedById { get; set; }
    public DateTimeOffset? CosignedAt { get; set; }

    public ICollection<NoteAddendum> Addenda { get; set; } = new List<NoteAddendum>();
    public ICollection<NoteIntervention> InterventionItems { get; set; } = new List<NoteIntervention>();

    public bool IsSigned => Status is NoteStatus.Signed or NoteStatus.Locked;

    /// <summary>Locked additionally blocks new addenda -- see NoteStatus.Locked's own doc comment.</summary>
    public bool IsLocked => Status == NoteStatus.Locked;

    public ICollection<ClinicalNoteVersion> Versions { get; set; } = new List<ClinicalNoteVersion>();
}

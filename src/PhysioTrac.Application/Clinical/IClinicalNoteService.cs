using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Clinical;

/// <summary>Direct port of `care/note_management.py` — clinical note
/// lifecycle: permission predicates plus the create/sign/cosign/addendum
/// transition functions. Both permission checks and mutations live behind
/// this one interface so a controller (or a future legacy surface) can't
/// duplicate the rules.</summary>
public interface IClinicalNoteService
{
    bool CanViewNote(ICurrentUser user, ClinicalNote note);

    /// <summary>A signed note is never editable; otherwise the author, or an admin/director.</summary>
    bool CanEditNote(ICurrentUser user, ClinicalNote note);

    /// <summary>Who may sign this specific note. Admin/director always; the
    /// owning therapist if their role carries sign-notes capability; the
    /// owning PTA/Assistant too — landing on ReviewRequired instead of
    /// Signed when the note requires cosign (see <see cref="SignNoteAsync"/>).</summary>
    bool CanFinalizeNote(ICurrentUser user, ClinicalNote note);

    /// <summary>Who may cosign a PTA-authored note awaiting review — a
    /// supervising admin/director/therapist, never the note's own author.</summary>
    bool CanCosignNote(ICurrentUser user, ClinicalNote note);

    bool CanCreateAddendum(ICurrentUser user, ClinicalNote note);

    /// <summary>Locking is a further step past Signed -- see NoteStatus.Locked's own doc comment.</summary>
    bool CanLockNote(ICurrentUser user, ClinicalNote note);

    Task<ClinicalNote> CreateDraftAsync(CreateNoteRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Throws <see cref="Common.ForbiddenException"/> if the note
    /// is signed or the caller isn't the author/an admin/director — callers
    /// must check <see cref="CanEditNote"/> themselves for a friendlier error,
    /// this is the last-line enforcement.</summary>
    Task<ClinicalNote> UpdateDraftAsync(Guid noteId, UpdateNoteRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<ClinicalNote> GetAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<ClinicalNote>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Finalizes a note. Caller must have already checked
    /// <see cref="CanFinalizeNote"/>. Throws if the compliance-finding
    /// blockers aren't resolved, or attestation isn't confirmed. Captures
    /// the signer's credentials-at-signing-time, ipAddress, and a content
    /// hash on the note itself, and writes the final immutable
    /// ClinicalNoteVersion snapshot.</summary>
    /// <param name="password">The signer's own password (step-up); a missing or
    /// wrong one throws <see cref="SignatureVerificationException"/>.</param>
    Task<ClinicalNote> SignNoteAsync(Guid noteId, bool attestationConfirmed, string? ipAddress, ICurrentUser actor, string? password = null, CancellationToken ct = default);

    /// <summary>A further, manual step past Signed that additionally blocks
    /// new addenda -- see NoteStatus.Locked's own doc comment.</summary>
    Task<ClinicalNote> LockNoteAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Every saved snapshot of the note's content, oldest first --
    /// every draft save plus the final signed version. Append-only; see
    /// ClinicalNoteVersion's own doc comment.</summary>
    Task<IReadOnlyList<ClinicalNoteVersion>> GetVersionHistoryAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Completes a PTA-authored note awaiting cosign. Caller must
    /// have already checked <see cref="CanCosignNote"/>.</summary>
    /// <param name="password">The cosigner's own password (step-up).</param>
    Task<ClinicalNote> CosignNoteAsync(Guid noteId, ICurrentUser actor, string? password = null, CancellationToken ct = default);

    /// <summary>Attach a correction to a signed note. The original note is never touched.</summary>
    Task<NoteAddendum> CreateAddendumAsync(Guid noteId, CreateAddendumRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<NoteIntervention> AddInterventionAsync(Guid noteId, CreateInterventionRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<NoteIntervention>> ListInterventionsAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Records the physician certification of this note's plan of
    /// care. One of the few writes allowed on an already-Signed/Locked note
    /// -- see EnforceSignedNoteImmutability and PlanOfCareCertifiedDate's
    /// own doc comment for why. Gated the same as finalizing (CanFinalizeNote),
    /// since certifying is itself a clinical sign-off responsibility.</summary>
    Task<ClinicalNote> CertifyPlanOfCareAsync(Guid noteId, CertifyPlanOfCareRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>What a new note for this patient should pre-populate --
    /// active goals, the most recent note's objective measurements, and
    /// active diagnoses. See PullForwardDataDto's own doc comment.</summary>
    Task<PullForwardDataDto> GetPullForwardDataAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Whether a progress note is due for this patient right now.
    /// See ProgressNoteStatusDto's own doc comment.</summary>
    Task<ProgressNoteStatusDto> GetProgressNoteStatusAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Clinical Charting: edit an intervention row on a draft note.</summary>
    Task<NoteIntervention> UpdateInterventionAsync(Guid noteId, Guid interventionId, CreateInterventionRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Clinical Charting: remove an intervention row from a draft note.</summary>
    Task DeleteInterventionAsync(Guid noteId, Guid interventionId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Total timed minutes and the organization's 8-minute-rule units.</summary>
    Task<InterventionSummaryDto> SummarizeInterventionsAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>A formal amendment may be started on a Signed (not Locked)
    /// note by whoever could sign it.</summary>
    bool CanAmendNote(ICurrentUser user, ClinicalNote note);

    /// <summary>Starts (or returns the already-open) formal amendment of a
    /// signed note: a new draft copied from it -- narrative, structured
    /// findings and interventions -- carrying the reason. Signing that draft
    /// supersedes the original (Status Amended); the original never changes.</summary>
    Task<ClinicalNote> CreateAmendmentAsync(Guid noteId, CreateAmendmentRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Actions, people, addenda and amendment links for one note.</summary>
    Task<NoteRecordDto> GetNoteRecordAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Version history with the saver's name on each version.</summary>
    Task<IReadOnlyList<ClinicalNoteVersionDto>> GetVersionHistoryViewAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The current user's documentation work queues.</summary>
    Task<NoteQueuesDto> GetNoteQueuesAsync(ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The note's electronic signatures and every status change.</summary>
    Task<NoteHistoryDto> GetNoteHistoryAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The note opened as an encounter: template version, values, header facts.</summary>
    Task<EncounterDto> GetEncounterAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Autosave of a draft encounter (template values + narrative),
    /// refused with <see cref="EncounterConflictException"/> when someone
    /// else saved since <see cref="SaveEncounterRequest.BaseSaveVersion"/>.</summary>
    Task<EncounterSaveResultDto> SaveEncounterAsync(Guid noteId, SaveEncounterRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Switches a draft to another template before any field is filled.</summary>
    Task<ClinicalNote> ChangeTemplateAsync(Guid noteId, Guid templateId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Documentation checks, including the template's required fields.</summary>
    Task<IReadOnlyList<ComplianceFinding>> GetComplianceAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The patient's plans of care, newest first.</summary>
    Task<IReadOnlyList<PlanOfCareDto>> ListPlansOfCareAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);
}

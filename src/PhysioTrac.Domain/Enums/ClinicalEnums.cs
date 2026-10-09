namespace PhysioTrac.Domain.Enums;

/// <summary>Dry Needling CONSENT is deliberately not a value here -- it's
/// already modeled as ConsentType.DryNeedlingConsent in the existing
/// Consent subsystem (a signed attestation, not a clinical note); only the
/// treatment note itself (DryNeedlingTreatment) belongs in this enum.</summary>
public enum NoteType
{
    Evaluation,
    Daily,
    Soap,
    HomeVisit,
    Progress,
    ReEvaluation,
    Discharge,
    Handoff,
    PlanOfCare,
    DryNeedlingTreatment,
    PelvicHealthEvaluation,
    Recertification,
    Consultation,
    Communication,
    MissedVisit,
    Addendum,
}

/// <summary>Locked is a further, manual step past Signed -- Signed already
/// blocks direct edits (see EnforceSignedNoteImmutability) and still allows
/// an addendum; Locked additionally blocks new addenda too (e.g. once a
/// billing cycle closes on the note). Amended: a signed note superseded by
/// its signed formal amendment (ClinicalNote.AmendsNoteId) -- content and
/// signature unchanged, still part of the legal record.</summary>
public enum NoteStatus
{
    Draft,
    ReviewRequired,
    Signed,
    Amended,
    Locked,
    /// <summary>A supervising PT sent an assistant's submitted note back
    /// with a reason; the assistant edits it again and resubmits.</summary>
    ReturnedForCorrection,
    /// <summary>Withdrawn with a reason (e.g. documented on the wrong
    /// patient). Kept, never deleted; excluded from the active record.</summary>
    Voided,
    /// <summary>A supervising PT has started reviewing a submitted
    /// (ReviewRequired) note; they cosign it or return it for correction.</summary>
    InReview,
}

public enum InterventionCategory
{
    TherapeuticExercise,
    ManualTherapy,
    TherapeuticActivity,
    NeuromuscularReeducation,
    GaitTraining,
    SelfCare,
    PatientEducation,
    Other,
    CanalithRepositioning,
    Modalities,
    DryNeedling,
    HomeExerciseProgram,
}

/// <summary>A goal's state. Draft goals await a PT's approval; approved
/// goals are Not Started, In Progress (Active), Met, Partially Met or
/// Discontinued. Values are stored, so new states are appended.</summary>
public enum GoalStatus
{
    Draft,
    /// <summary>In progress.</summary>
    Active,
    Met,
    Discontinued,
    NotStarted,
    PartiallyMet,
}

public enum GoalTerm
{
    ShortTerm,
    LongTerm,
}

/// <summary>Where a clinical note template applies -- most-specific-wins
/// resolution: Location, then State, then Organization, then Platform
/// (the only scope with no OrganizationId, visible to every tenant as a
/// fallback default). See ClinicalTemplateService.ResolveAsync.</summary>
public enum TemplateScope
{
    Platform,
    Organization,
    State,
    Location,
}

public enum OutcomeMeasure
{
    Lefs,
    Odi,
    Ndi,
    QuickDash,
    Tug,
    Berg,
    Psfs,
    FiveTimesSitToStand,
    Abc,
    Fga,
}

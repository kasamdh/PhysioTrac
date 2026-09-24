namespace PhysioTrac.Domain.Enums;

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
}

/// <summary>Locked is a further, manual step past Signed -- Signed already
/// blocks direct edits (see EnforceSignedNoteImmutability) and still allows
/// an addendum; Locked additionally blocks new addenda too (e.g. once a
/// billing cycle closes on the note). Amended is defined but not yet wired
/// to any transition -- pre-existing, unrelated to this phase's changes.</summary>
public enum NoteStatus
{
    Draft,
    ReviewRequired,
    Signed,
    Amended,
    Locked,
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
}

public enum GoalStatus
{
    Draft,
    Active,
    Met,
    Discontinued,
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
}

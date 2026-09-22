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

public enum NoteStatus
{
    Draft,
    ReviewRequired,
    Signed,
    Amended,
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

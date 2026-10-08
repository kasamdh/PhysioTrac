namespace PhysioTrac.Domain.Enums;

/// <summary>Clinical specialty a documentation template (or special test,
/// later) is meant for. General = usable by every specialty.</summary>
public enum ClinicalSpecialty
{
    General,
    Orthopedic,
    Neurological,
    PelvicHealth,
    SportsRehabilitation,
    PostSurgical,
    GaitAndBalance,
    Vestibular,
    Cardiopulmonary,
    Tmj,
    PersistentPain,
    DryNeedling,
}

/// <summary>How a template field is entered and stored. Values of
/// ShortText/LongText/Select/Radio go to ValueText, Number/Pain scale to
/// ValueNumber, Date to ValueDate, Time to ValueTime, Checkbox to
/// ValueBool; only Multiselect and StructuredTable use ValueJson (their
/// shape is genuinely dynamic). ClinicalMeasurement carries a unit.
/// Signature marks where the note's electronic signature appears -- it is
/// never typed in; the signature itself is an ElectronicSignature row.</summary>
public enum TemplateFieldType
{
    ShortText,
    LongText,
    Number,
    Date,
    Time,
    Checkbox,
    Radio,
    Select,
    Multiselect,
    ClinicalMeasurement,
    PainScale,
    StructuredTable,
    Signature,
}

/// <summary>What an electronic signature attests to.</summary>
public enum SignatureMeaning
{
    /// <summary>The treating clinician authored and finalized the note.</summary>
    Author,
    /// <summary>A supervising PT reviewed and cosigned an assistant's note.</summary>
    Cosign,
    /// <summary>The note is a signed amendment of an earlier signed note.</summary>
    Amendment,
    /// <summary>The signer voided the note (with a reason).</summary>
    Void,
}

public enum CosignRequestStatus
{
    Pending,
    Cosigned,
    Returned,
    Cancelled,
}

public enum PlanOfCareStatus
{
    /// <summary>Created from an evaluation that isn't signed yet.</summary>
    Draft,
    Active,
    /// <summary>Replaced by a newer signed plan (re-evaluation / recertification).</summary>
    Superseded,
    /// <summary>Closed by a signed discharge summary.</summary>
    Discharged,
    Expired,
    /// <summary>Its source note was voided.</summary>
    Voided,
}

public enum DischargeReason
{
    GoalsMet,
    MaximumBenefitAchieved,
    PatientRequest,
    Nonattendance,
    MedicalChange,
    ReferredElsewhere,
    AuthorizationLimitation,
    Other,
}

/// <summary>Kinds of library items a clinician can mark as a favorite.</summary>
public enum FavoriteItemType
{
    Template,
    SpecialTest,
    Intervention,
    InterventionGroup,
    OutcomeMeasure,
}

/// <summary>The scale a pain rating is recorded on.</summary>
public enum PainScaleType
{
    /// <summary>0-10 numeric rating.</summary>
    NumericRating,
    /// <summary>0-100 mm visual analog scale.</summary>
    VisualAnalog,
    /// <summary>Wong-Baker faces: 0, 2, 4, 6, 8, 10.</summary>
    Faces,
    /// <summary>0 none, 1 mild, 2 moderate, 3 severe.</summary>
    Verbal,
}

public enum PainFrequency
{
    Constant,
    Intermittent,
    Occasional,
}

public enum PainIrritability
{
    Low,
    Moderate,
    High,
}

public enum BodyView
{
    Front,
    Back,
}

/// <summary>The patient's side (not the viewer's).</summary>
public enum BodySide
{
    Left,
    Right,
    Bilateral,
    Midline,
}

public enum BodyFindingType
{
    Pain,
    Numbness,
    Tingling,
    Burning,
    Swelling,
    Tenderness,
    Incision,
    Scar,
    Radiating,
    Other,
}

public enum MeasurementCategory
{
    RangeOfMotion,
    Strength,
    Sensation,
    Dermatome,
    Myotome,
    Reflex,
    Tone,
    Coordination,
    CranialNerve,
    Gait,
    Balance,
    Functional,
}

/// <summary>What a special test's result consists of.</summary>
public enum SpecialTestResultKind
{
    PositiveNegative,
    Numeric,
    Both,
}

public enum SpecialTestOutcome
{
    NotTested,
    Positive,
    Negative,
}

/// <summary>What happened with a flowsheet entry this visit.</summary>
public enum InterventionStatus
{
    Completed,
    Modified,
    Held,
    Discontinued,
}

/// <summary>What a goal history entry records.</summary>
public enum GoalHistoryKind
{
    Created,
    Approved,
    Edited,
    Progress,
}

namespace PhysioTrac.Domain.Enums;

/// <summary>Where an exercise works. Values are stored -- append only.</summary>
public enum ExerciseBodyRegion
{
    NeckCervical,
    Shoulder,
    ElbowWristHand,
    ThoracicSpine,
    LowBack,
    Hip,
    Knee,
    AnkleFoot,
    Core,
    Pelvis,
    WholeBody,
}

/// <summary>The kind of exercise. Values are stored -- append only.</summary>
public enum ExerciseCategory
{
    NeckCervicalSpine,
    Shoulder,
    ElbowWristHand,
    ThoracicSpine,
    LowBack,
    Hip,
    Knee,
    AnkleFoot,
    CoreStrengthening,
    BalanceCoordination,
    GaitTraining,
    PostureErgonomics,
    StretchingFlexibility,
    Strengthening,
    RangeOfMotion,
    NeuromuscularReeducation,
    PostOperative,
    PelvicHealth,
    SportsRehabilitation,
    FallPrevention,
    HomeSafety,
}

public enum ExerciseDifficulty
{
    Beginner,
    Intermediate,
    Advanced,
}

public enum ExercisePosition
{
    Standing,
    Sitting,
    Supine,
    Prone,
    SideLying,
    Kneeling,
    Other,
}

/// <summary>Which side the exercise is done on (a prescription can narrow
/// a bilateral exercise to one side).</summary>
public enum ExerciseLaterality
{
    NotApplicable,
    Left,
    Right,
    Bilateral,
    EitherSide,
}

using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One objective measurement on a note (a ROM reading, a strength
/// grade, a reflex, a gait or balance result...). Rows with the same
/// category, item, movement, side, mode and unit are the same measurement
/// across visits -- that is how baseline, previous and change are found.</summary>
public class ObjectiveMeasurement : BaseEntity, INoteOwned
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid PatientId { get; set; }

    public MeasurementCategory Category { get; set; }
    public string? BodyRegion { get; set; }

    /// <summary>Joint, muscle, nerve level, gait/balance item or activity.</summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>Movement or detail (e.g. "Flexion", "L4", "Eyes closed on foam").</summary>
    public string? Movement { get; set; }

    public BodySide? Side { get; set; }

    /// <summary>How it was measured: AROM, PROM, MMT, Dynamometer, Observed...</summary>
    public string? Mode { get; set; }

    public decimal? NumericValue { get; set; }

    /// <summary>A graded or descriptive result: MMT "4-", reflex "2+", "Intact".</summary>
    public string? TextValue { get; set; }

    public string? Unit { get; set; }
    public string? EndFeel { get; set; }
    public bool? Painful { get; set; }
    public string? Compensation { get; set; }
    public string? AssistiveDevice { get; set; }
    public string? AssistanceLevel { get; set; }
    public string? Surface { get; set; }
    public string? Condition { get; set; }
    public string? Comment { get; set; }
    public int Order { get; set; }
}

/// <summary>A special test in the clinic's library. System definitions ship
/// with the application; inactive ones are no longer offered for new notes
/// but past results keep their own copy of the name.</summary>
public class SpecialTestDefinition : BaseEntity, IUserStamped
{
    /// <summary>Stable code for system definitions (e.g. "lachman").</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ClinicalSpecialty Specialty { get; set; }
    public string? BodyRegion { get; set; }
    public string? Description { get; set; }
    public SpecialTestResultKind ResultKind { get; set; } = SpecialTestResultKind.PositiveNegative;
    public string? Unit { get; set; }

    /// <summary>Reference for the therapist (what a positive result may
    /// indicate). Shown as guidance; the system never draws a conclusion.</summary>
    public string? InterpretationGuide { get; set; }

    /// <summary>Shown prominently before the test is recorded.</summary>
    public string? ContraindicationWarning { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>A special test performed on a note.</summary>
public class SpecialTestResult : BaseEntity, INoteOwned
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid PatientId { get; set; }

    public Guid? DefinitionId { get; set; }
    public SpecialTestDefinition? Definition { get; set; }

    /// <summary>Copied at recording so the result reads the same even if the
    /// definition is renamed or retired.</summary>
    public string TestName { get; set; } = string.Empty;
    public ClinicalSpecialty Specialty { get; set; }
    public string? BodyRegion { get; set; }
    public BodySide? Side { get; set; }
    public SpecialTestOutcome Outcome { get; set; } = SpecialTestOutcome.NotTested;
    public decimal? NumericValue { get; set; }
    public string? Unit { get; set; }

    /// <summary>The therapist's interpretation (never generated).</summary>
    public string? Interpretation { get; set; }
    public string? Comment { get; set; }
    public int Order { get; set; }
}

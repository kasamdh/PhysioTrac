using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A therapeutic exercise in the Home Exercise Program library:
/// the patient-facing description, instructions, safety information and
/// images. Separate from <see cref="InterventionLibraryItem"/> (what is
/// done and billed in clinic). A null <see cref="OrganizationId"/> is a
/// platform exercise every clinic sees and only the platform can change;
/// otherwise it belongs to that clinic alone.</summary>
public class Exercise : BaseEntity, IUserStamped
{
    public Guid? OrganizationId { get; set; }

    /// <summary>Stable code for platform exercises (e.g. "chin-tuck").</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? PatientDescription { get; set; }
    public string? ClinicalPurpose { get; set; }
    public ExerciseBodyRegion BodyRegion { get; set; }
    public string? TargetMuscles { get; set; }
    public ExerciseCategory Category { get; set; }

    public string? StartingPosition { get; set; }
    /// <summary>Step-by-step instructions, one step per line.</summary>
    public string? Instructions { get; set; }
    public string? EndingPosition { get; set; }

    public string? Equipment { get; set; }
    public ExerciseDifficulty Difficulty { get; set; }
    public ExercisePosition Position { get; set; }
    public ExerciseLaterality Laterality { get; set; }

    public string? BreathingInstructions { get; set; }
    public string? CommonMistakes { get; set; }
    public string? SafetyPrecautions { get; set; }
    public string? Contraindications { get; set; }
    public string? Progressions { get; set; }
    public string? Regressions { get; set; }

    /// <summary>Optional instructional video (https only).</summary>
    public string? VideoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Platform starter content written without clinical sign-off:
    /// shown with a "Needs clinical review" label until a clinician
    /// reviews it.</summary>
    public bool NeedsClinicalReview { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public ICollection<ExerciseMedia> Media { get; set; } = new List<ExerciseMedia>();
}

/// <summary>An image of an exercise (a single illustration, or one step of
/// a sequence). Never changed in place: replacing an image adds a new row
/// and retires the old one, so a published home exercise program keeps
/// showing exactly the image it was published with. Files live in
/// <c>IFileStorage</c> under opaque keys and are only served through an
/// authorized endpoint -- never a public URL.</summary>
public class ExerciseMedia : BaseEntity
{
    public Guid ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    /// <summary>Copied from the exercise (null = platform image).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The re-encoded display image.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string ThumbnailStorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Order in the sequence (1 = starting position).</summary>
    public int Sequence { get; set; }
    public string AltText { get; set; } = string.Empty;
    /// <summary>Step caption, e.g. "Starting position".</summary>
    public string? Caption { get; set; }
    /// <summary>Where the image came from and its licence (e.g. "Drawn by
    /// clinic staff" / "Licensed from ...").</summary>
    public string? SourceAttribution { get; set; }
    /// <summary>The uploaded file's name, cleaned (never used as a path).</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>Set when replaced or removed; kept for programs that used it.</summary>
    public DateTimeOffset? RetiredAt { get; set; }
    public Guid? ReplacedByMediaId { get; set; }

    public Guid CreatedById { get; set; }

    public bool IsCurrent => RetiredAt is null;
}

using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>One image of an exercise, as the library and builder show it.</summary>
public record ExerciseMediaDto(
    Guid Id, int Sequence, string AltText, string? Caption, string? SourceAttribution,
    int Width, int Height, string ContentType, bool IsCurrent, DateTimeOffset CreatedAt);

/// <summary>A library card: enough to search and pick an exercise.</summary>
public record ExerciseSummaryDto(
    Guid Id, string Code, string Name, string? PatientDescription, ExerciseBodyRegion BodyRegion, ExerciseCategory Category,
    string? Equipment, ExerciseDifficulty Difficulty, ExercisePosition Position, bool IsActive, bool IsPlatform,
    bool NeedsClinicalReview, ExerciseMediaDto? CoverImage, int ImageCount);

public record ExerciseDto(
    Guid Id, string Code, string Name, string? PatientDescription, string? ClinicalPurpose,
    ExerciseBodyRegion BodyRegion, string? TargetMuscles, ExerciseCategory Category,
    string? StartingPosition, string? Instructions, string? EndingPosition, string? Equipment,
    ExerciseDifficulty Difficulty, ExercisePosition Position, ExerciseLaterality Laterality,
    string? BreathingInstructions, string? CommonMistakes, string? SafetyPrecautions, string? Contraindications,
    string? Progressions, string? Regressions, string? VideoUrl,
    bool IsActive, bool IsPlatform, bool NeedsClinicalReview, DateTimeOffset? ReviewedAt, string? ReviewedByName,
    bool CanEdit, IReadOnlyList<ExerciseMediaDto> Images,
    DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset UpdatedAt, string? UpdatedByName);

public record SaveExerciseRequest(
    string Name, string? PatientDescription, string? ClinicalPurpose, ExerciseBodyRegion BodyRegion, string? TargetMuscles,
    ExerciseCategory Category, string? StartingPosition, string? Instructions, string? EndingPosition, string? Equipment,
    ExerciseDifficulty Difficulty, ExercisePosition Position, ExerciseLaterality Laterality,
    string? BreathingInstructions, string? CommonMistakes, string? SafetyPrecautions, string? Contraindications,
    string? Progressions, string? Regressions, string? VideoUrl);

public record ExerciseSearch(
    string? Text = null, ExerciseBodyRegion? BodyRegion = null, ExerciseCategory? Category = null, string? Equipment = null,
    ExerciseDifficulty? Difficulty = null, ExercisePosition? Position = null, bool IncludeInactive = false);

/// <summary>An uploaded image. The stream is read once, checked by content
/// (not by name or browser type) and re-encoded before anything is stored.</summary>
public record UploadExerciseImage(
    Stream Content, long Length, string? FileName, string AltText, string? Caption, string? SourceAttribution, int? Sequence = null);

public record UpdateExerciseImageRequest(string AltText, string? Caption, string? SourceAttribution);

/// <summary>A decoded and re-encoded image (display size and thumbnail).</summary>
public record ProcessedImage(byte[] Display, byte[] Thumbnail, string ContentType, int Width, int Height);

/// <summary>Checks an uploaded file really is a JPEG, PNG or WebP image and
/// re-encodes it, which strips embedded metadata (EXIF location, comments)
/// and anything hidden after the image data.</summary>
public interface IExerciseImageProcessor
{
    /// <exception cref="InvalidOperationException">Not a supported image, or too small/large.</exception>
    ProcessedImage Process(byte[] upload);
}

public interface IExerciseLibraryService
{
    Task<IReadOnlyList<ExerciseSummaryDto>> SearchAsync(ExerciseSearch search, ICurrentUser actor, CancellationToken ct = default);
    Task<ExerciseDto> GetAsync(Guid id, ICurrentUser actor, CancellationToken ct = default);
    Task<ExerciseDto> CreateAsync(SaveExerciseRequest request, ICurrentUser actor, CancellationToken ct = default);
    Task<ExerciseDto> UpdateAsync(Guid id, SaveExerciseRequest request, ICurrentUser actor, CancellationToken ct = default);
    Task<ExerciseDto> SetActiveAsync(Guid id, bool isActive, ICurrentUser actor, CancellationToken ct = default);
    /// <summary>A clinician confirms the written content is clinically correct.</summary>
    Task<ExerciseDto> MarkReviewedAsync(Guid id, ICurrentUser actor, CancellationToken ct = default);

    Task<ExerciseMediaDto> AddImageAsync(Guid exerciseId, UploadExerciseImage upload, ICurrentUser actor, CancellationToken ct = default);
    /// <summary>A new image takes the old one's place in the sequence; the old one is retired, not deleted.</summary>
    Task<ExerciseMediaDto> ReplaceImageAsync(Guid exerciseId, Guid mediaId, UploadExerciseImage upload, ICurrentUser actor, CancellationToken ct = default);
    Task<ExerciseMediaDto> UpdateImageAsync(Guid exerciseId, Guid mediaId, UpdateExerciseImageRequest request, ICurrentUser actor, CancellationToken ct = default);
    /// <summary>Retires the image (kept for programs that already used it).</summary>
    Task RemoveImageAsync(Guid exerciseId, Guid mediaId, ICurrentUser actor, CancellationToken ct = default);
    /// <summary>Sets the sequence to the given order of current image ids.</summary>
    Task<IReadOnlyList<ExerciseMediaDto>> ReorderImagesAsync(Guid exerciseId, IReadOnlyList<Guid> mediaIds, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Opens an image for an authorized viewer (thumbnail or display size).</summary>
    Task<(Stream Content, string ContentType)> OpenImageAsync(Guid exerciseId, Guid mediaId, bool thumbnail, ICurrentUser actor, CancellationToken ct = default);
}

public static class ExerciseRules
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const int MaxImagesPerExercise = 8;
    public const int MinImageSide = 100;
    public const int MaxImageSide = 8000;
    /// <summary>Display images are scaled down to fit this (long side).</summary>
    public const int DisplaySide = 1600;
    public const int ThumbnailSide = 400;
}

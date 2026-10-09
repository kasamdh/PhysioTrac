using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Documents;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>The Home Exercise Program exercise library and its images.
/// Staff see platform exercises plus their own clinic's. A clinic's
/// admins, directors and therapists change only their clinic's exercises;
/// platform exercises are changed only by the platform super admin -- held
/// even while role checks are off, since every clinic shares them. Images
/// are never edited in place (see <see cref="ExerciseMedia"/>).</summary>
public class ExerciseLibraryService : IExerciseLibraryService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;
    private readonly IFileStorage _storage;
    private readonly IExerciseImageProcessor _images;

    public ExerciseLibraryService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit, IFileStorage storage,
        IExerciseImageProcessor images)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
        _storage = storage;
        _images = images;
    }

    // ------------------------------------------------------------------ scope

    /// <summary>The caller's clinic, or null for a platform super admin
    /// working outside any clinic.</summary>
    private async Task<Guid?> ScopeAsync(ICurrentUser actor, CancellationToken ct)
    {
        if (actor.IsPlatformSuperAdmin && actor.OrganizationId is null) return null;
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        return (await _tenantAccess.OrganizationRequiredAsync(actor, ct)).Id;
    }

    private IQueryable<Exercise> Visible(Guid? organizationId) =>
        _db.Exercises.Where(x => x.OrganizationId == null || x.OrganizationId == organizationId);

    private bool CanEdit(ICurrentUser actor, Exercise exercise, Guid? scope) =>
        exercise.OrganizationId is null
            ? actor.IsPlatformSuperAdmin
            : exercise.OrganizationId == scope && (actor.IsPlatformSuperAdmin || RoleAllows(actor, RoleSets.ExerciseLibrary));

    private bool RoleAllows(ICurrentUser actor, IReadOnlySet<UserRole> roles)
    {
        try { _tenantAccess.RequireRole(actor, roles); return true; }
        catch (ForbiddenException) { return false; }
    }

    private async Task<(Exercise Exercise, Guid? Scope)> LoadAsync(Guid id, ICurrentUser actor, bool forEdit, CancellationToken ct)
    {
        var scope = await ScopeAsync(actor, ct);
        var exercise = await Visible(scope).Include(x => x.Media).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Exercise was not found.");
        if (forEdit && !CanEdit(actor, exercise, scope))
            throw new ForbiddenException(exercise.OrganizationId is null
                ? "Platform exercises can only be changed by the platform administrator."
                : "You are not permitted to change this exercise.");
        return (exercise, scope);
    }

    // ------------------------------------------------------------------ exercises

    public async Task<IReadOnlyList<ExerciseSummaryDto>> SearchAsync(ExerciseSearch search, ICurrentUser actor, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(actor, ct);
        var query = Visible(scope).AsNoTracking();
        if (!search.IncludeInactive) query = query.Where(x => x.IsActive);
        if (search.BodyRegion is { } region) query = query.Where(x => x.BodyRegion == region);
        if (search.Category is { } category) query = query.Where(x => x.Category == category);
        if (search.Difficulty is { } difficulty) query = query.Where(x => x.Difficulty == difficulty);
        if (search.Position is { } position) query = query.Where(x => x.Position == position);
        if (!string.IsNullOrWhiteSpace(search.Equipment))
        {
            var equipment = $"%{SpecialTestLibraryService.EscapeLike(search.Equipment.Trim())}%";
            query = query.Where(x => x.Equipment != null && EF.Functions.Like(x.Equipment, equipment, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var like = $"%{SpecialTestLibraryService.EscapeLike(search.Text.Trim())}%";
            query = query.Where(x => EF.Functions.Like(x.Name, like, "\\") ||
                (x.TargetMuscles != null && EF.Functions.Like(x.TargetMuscles, like, "\\")) ||
                (x.Equipment != null && EF.Functions.Like(x.Equipment, like, "\\")) ||
                (x.PatientDescription != null && EF.Functions.Like(x.PatientDescription, like, "\\")));
        }
        var exercises = await query.OrderBy(x => x.Name).Take(300).ToListAsync(ct);
        var ids = exercises.Select(x => x.Id).ToList();
        var media = (await _db.ExerciseMedia.AsNoTracking().Where(m => ids.Contains(m.ExerciseId) && m.RetiredAt == null).ToListAsync(ct))
            .GroupBy(m => m.ExerciseId).ToDictionary(g => g.Key, g => g.OrderBy(m => m.Sequence).ToList());
        return exercises.Select(x =>
        {
            var images = media.GetValueOrDefault(x.Id) ?? [];
            return new ExerciseSummaryDto(x.Id, x.Code, x.Name, x.PatientDescription, x.BodyRegion, x.Category, x.Equipment, x.Difficulty,
                x.Position, x.IsActive, x.OrganizationId is null, x.NeedsClinicalReview, images.Select(ToDto).FirstOrDefault(), images.Count);
        }).ToList();
    }

    public async Task<ExerciseDto> GetAsync(Guid id, ICurrentUser actor, CancellationToken ct = default)
    {
        var (exercise, scope) = await LoadAsync(id, actor, forEdit: false, ct);
        return await ToDtoAsync(exercise, CanEdit(actor, exercise, scope), ct);
    }

    public async Task<ExerciseDto> CreateAsync(SaveExerciseRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(actor, ct);
        // A platform super admin adds platform exercises; everyone else adds their clinic's.
        var organizationId = actor.IsPlatformSuperAdmin ? null : scope;
        if (organizationId is not null) _tenantAccess.RequireRole(actor, RoleSets.ExerciseLibrary);
        Validate(request);

        var exercise = new Exercise { OrganizationId = organizationId, Code = await UniqueCodeAsync(request.Name, organizationId, ct) };
        Apply(exercise, request);
        _db.Exercises.Add(exercise);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise.created", new { exerciseId = exercise.Id }, ct);
        return await ToDtoAsync(exercise, true, ct);
    }

    public async Task<ExerciseDto> UpdateAsync(Guid id, SaveExerciseRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(id, actor, forEdit: true, ct);
        Validate(request);
        Apply(exercise, request);
        exercise.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise.updated", new { exerciseId = exercise.Id }, ct);
        return await ToDtoAsync(exercise, true, ct);
    }

    public async Task<ExerciseDto> SetActiveAsync(Guid id, bool isActive, ICurrentUser actor, CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(id, actor, forEdit: true, ct);
        exercise.IsActive = isActive;
        exercise.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, isActive ? "exercise.activated" : "exercise.deactivated", new { exerciseId = exercise.Id }, ct);
        return await ToDtoAsync(exercise, true, ct);
    }

    public async Task<ExerciseDto> MarkReviewedAsync(Guid id, ICurrentUser actor, CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(id, actor, forEdit: true, ct);
        exercise.NeedsClinicalReview = false;
        exercise.ReviewedAt = DateTimeOffset.UtcNow;
        exercise.ReviewedById = actor.UserId;
        exercise.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise.reviewed", new { exerciseId = exercise.Id }, ct);
        return await ToDtoAsync(exercise, true, ct);
    }

    // ------------------------------------------------------------------ images

    public async Task<ExerciseMediaDto> AddImageAsync(Guid exerciseId, UploadExerciseImage upload, ICurrentUser actor, CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(exerciseId, actor, forEdit: true, ct);
        var current = exercise.Media.Where(m => m.IsCurrent).OrderBy(m => m.Sequence).ToList();
        if (current.Count >= ExerciseRules.MaxImagesPerExercise)
            throw new InvalidOperationException($"An exercise can have up to {ExerciseRules.MaxImagesPerExercise} images.");

        var media = await StoreAsync(exercise, upload, actor, ct);
        // Inserting at a position moves later steps down one.
        var position = Math.Clamp(upload.Sequence ?? current.Count + 1, 1, current.Count + 1);
        foreach (var m in current.Where(m => m.Sequence >= position)) m.Sequence++;
        media.Sequence = position;
        _db.ExerciseMedia.Add(media);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise_media.uploaded", new { exerciseId = exercise.Id, mediaId = media.Id, sequence = media.Sequence }, ct);
        return ToDto(media);
    }

    public async Task<ExerciseMediaDto> ReplaceImageAsync(Guid exerciseId, Guid mediaId, UploadExerciseImage upload, ICurrentUser actor,
        CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(exerciseId, actor, forEdit: true, ct);
        var old = exercise.Media.FirstOrDefault(m => m.Id == mediaId && m.IsCurrent) ?? throw new NotFoundException("Image was not found.");
        var media = await StoreAsync(exercise, upload, actor, ct);
        media.Sequence = old.Sequence;
        old.RetiredAt = DateTimeOffset.UtcNow;
        old.ReplacedByMediaId = media.Id;
        old.UpdatedAt = DateTimeOffset.UtcNow;
        _db.ExerciseMedia.Add(media);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise_media.replaced",
            new { exerciseId = exercise.Id, mediaId = media.Id, replacedMediaId = old.Id, sequence = media.Sequence }, ct);
        return ToDto(media);
    }

    public async Task<ExerciseMediaDto> UpdateImageAsync(Guid exerciseId, Guid mediaId, UpdateExerciseImageRequest request, ICurrentUser actor,
        CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(exerciseId, actor, forEdit: true, ct);
        var media = exercise.Media.FirstOrDefault(m => m.Id == mediaId && m.IsCurrent) ?? throw new NotFoundException("Image was not found.");
        media.AltText = RequireAltText(request.AltText);
        media.Caption = Limit(request.Caption, 150, "The caption");
        media.SourceAttribution = Limit(request.SourceAttribution, 300, "The source");
        media.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise_media.updated", new { exerciseId = exercise.Id, mediaId = media.Id }, ct);
        return ToDto(media);
    }

    public async Task RemoveImageAsync(Guid exerciseId, Guid mediaId, ICurrentUser actor, CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(exerciseId, actor, forEdit: true, ct);
        var media = exercise.Media.FirstOrDefault(m => m.Id == mediaId && m.IsCurrent) ?? throw new NotFoundException("Image was not found.");
        media.RetiredAt = DateTimeOffset.UtcNow;
        media.UpdatedAt = DateTimeOffset.UtcNow;
        Resequence(exercise.Media.Where(m => m.IsCurrent).OrderBy(m => m.Sequence));
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise_media.removed", new { exerciseId = exercise.Id, mediaId = media.Id }, ct);
    }

    public async Task<IReadOnlyList<ExerciseMediaDto>> ReorderImagesAsync(Guid exerciseId, IReadOnlyList<Guid> mediaIds, ICurrentUser actor,
        CancellationToken ct = default)
    {
        var (exercise, _) = await LoadAsync(exerciseId, actor, forEdit: true, ct);
        var current = exercise.Media.Where(m => m.IsCurrent).ToList();
        if (mediaIds.Count != current.Count || mediaIds.Distinct().Count() != current.Count || mediaIds.Any(id => current.All(m => m.Id != id)))
            throw new InvalidOperationException("List every current image of the exercise exactly once.");
        Resequence(mediaIds.Select(id => current.Single(m => m.Id == id)));
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, exercise, "exercise_media.reordered", new { exerciseId = exercise.Id, order = mediaIds }, ct);
        return current.OrderBy(m => m.Sequence).Select(ToDto).ToList();
    }

    public async Task<(Stream Content, string ContentType)> OpenImageAsync(Guid exerciseId, Guid mediaId, bool thumbnail, ICurrentUser actor,
        CancellationToken ct = default)
    {
        var scope = await ScopeAsync(actor, ct);
        var media = await _db.ExerciseMedia.AsNoTracking()
            .Where(m => m.Id == mediaId && m.ExerciseId == exerciseId && (m.OrganizationId == null || m.OrganizationId == scope))
            .Select(m => new { m.StorageKey, m.ThumbnailStorageKey, m.ContentType })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Image was not found.");
        return (await _storage.OpenReadAsync(thumbnail ? media.ThumbnailStorageKey : media.StorageKey, ct), media.ContentType);
    }

    private async Task<ExerciseMedia> StoreAsync(Exercise exercise, UploadExerciseImage upload, ICurrentUser actor, CancellationToken ct)
    {
        var altText = RequireAltText(upload.AltText);
        var caption = Limit(upload.Caption, 150, "The caption");
        var attribution = Limit(upload.SourceAttribution, 300, "The source");
        if (upload.Length <= 0) throw new InvalidOperationException("Choose an image to upload.");
        if (upload.Length > ExerciseRules.MaxImageBytes)
            throw new InvalidOperationException($"Images can be up to {ExerciseRules.MaxImageBytes / (1024 * 1024)} MB.");

        // Read at most one byte past the limit, whatever length was declared.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await upload.Content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > ExerciseRules.MaxImageBytes)
                throw new InvalidOperationException($"Images can be up to {ExerciseRules.MaxImageBytes / (1024 * 1024)} MB.");
        }
        var processed = _images.Process(buffer.ToArray());

        var displayKey = await _storage.SaveAsync(new MemoryStream(processed.Display), ct);
        string thumbKey;
        try { thumbKey = await _storage.SaveAsync(new MemoryStream(processed.Thumbnail), ct); }
        catch { await _storage.DeleteAsync(displayKey, CancellationToken.None); throw; }

        return new ExerciseMedia
        {
            ExerciseId = exercise.Id,
            OrganizationId = exercise.OrganizationId,
            StorageKey = displayKey,
            ThumbnailStorageKey = thumbKey,
            ContentType = processed.ContentType,
            SizeBytes = processed.Display.Length,
            Width = processed.Width,
            Height = processed.Height,
            AltText = altText,
            Caption = caption,
            SourceAttribution = attribution,
            OriginalFileName = SafeFileName(upload.FileName),
            CreatedById = actor.UserId,
        };
    }

    private static void Resequence(IEnumerable<ExerciseMedia> ordered)
    {
        var n = 1;
        foreach (var m in ordered) { m.Sequence = n++; m.UpdatedAt = DateTimeOffset.UtcNow; }
    }

    // ------------------------------------------------------------------ helpers

    private static void Validate(SaveExerciseRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name)) errors.Add("Name is required.");
        else if (r.Name.Trim().Length > 150) errors.Add("The name is 150 characters at most.");
        if (!Enum.IsDefined(r.BodyRegion)) errors.Add("Choose a body region.");
        if (!Enum.IsDefined(r.Category)) errors.Add("Choose a category.");
        if (!Enum.IsDefined(r.Difficulty)) errors.Add("Choose a difficulty.");
        if (!Enum.IsDefined(r.Position)) errors.Add("Choose a position.");
        if (!Enum.IsDefined(r.Laterality)) errors.Add("Choose the side.");
        if ((r.TargetMuscles?.Length ?? 0) > 300 || (r.Equipment?.Length ?? 0) > 300) errors.Add("Target muscles and equipment are 300 characters at most.");
        if ((r.Instructions?.Length ?? 0) > 4000) errors.Add("Instructions are 4000 characters at most.");
        foreach (var (label, text) in new[] { ("Description", r.PatientDescription), ("Clinical purpose", r.ClinicalPurpose),
            ("Starting position", r.StartingPosition), ("Ending position", r.EndingPosition), ("Breathing", r.BreathingInstructions),
            ("Common mistakes", r.CommonMistakes), ("Safety precautions", r.SafetyPrecautions), ("Contraindications", r.Contraindications),
            ("Progressions", r.Progressions), ("Regressions", r.Regressions) })
            if ((text?.Length ?? 0) > 2000) errors.Add($"{label} is 2000 characters at most.");
        if (!string.IsNullOrWhiteSpace(r.VideoUrl) &&
            !(Uri.TryCreate(r.VideoUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && r.VideoUrl.Trim().Length <= 500))
            errors.Add("The video link must be an https:// address.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }

    private static void Apply(Exercise x, SaveExerciseRequest r)
    {
        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        x.Name = r.Name.Trim();
        x.PatientDescription = Clean(r.PatientDescription);
        x.ClinicalPurpose = Clean(r.ClinicalPurpose);
        x.BodyRegion = r.BodyRegion;
        x.TargetMuscles = Clean(r.TargetMuscles);
        x.Category = r.Category;
        x.StartingPosition = Clean(r.StartingPosition);
        x.Instructions = Clean(r.Instructions);
        x.EndingPosition = Clean(r.EndingPosition);
        x.Equipment = Clean(r.Equipment);
        x.Difficulty = r.Difficulty;
        x.Position = r.Position;
        x.Laterality = r.Laterality;
        x.BreathingInstructions = Clean(r.BreathingInstructions);
        x.CommonMistakes = Clean(r.CommonMistakes);
        x.SafetyPrecautions = Clean(r.SafetyPrecautions);
        x.Contraindications = Clean(r.Contraindications);
        x.Progressions = Clean(r.Progressions);
        x.Regressions = Clean(r.Regressions);
        x.VideoUrl = Clean(r.VideoUrl);
    }

    private static string RequireAltText(string? altText)
    {
        if (string.IsNullOrWhiteSpace(altText)) throw new InvalidOperationException("Describe the image (alternative text) for people who can't see it.");
        return Limit(altText, 300, "The image description")!;
    }

    private static string? Limit(string? text, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (t.Length > max) throw new InvalidOperationException($"{label} is {max} characters at most.");
        return t;
    }

    /// <summary>The uploaded name kept for reference only: no path, only
    /// plain characters, never used to read or write a file.</summary>
    public static string? SafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var baseName = name.Replace('\\', '/').Split('/').Last();
        var cleaned = Regex.Replace(baseName, @"[^A-Za-z0-9._ -]", "_").Trim(' ', '.');
        return cleaned.Length == 0 ? null : cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }

    private async Task<string> UniqueCodeAsync(string name, Guid? organizationId, CancellationToken ct)
    {
        var slug = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "exercise";
        if (slug.Length > 70) slug = slug[..70].TrimEnd('-');
        var code = slug;
        for (var n = 2; await _db.Exercises.AnyAsync(x => x.OrganizationId == organizationId && x.Code == code, ct); n++) code = $"{slug}-{n}";
        return code;
    }

    private async Task AuditAsync(ICurrentUser actor, Exercise exercise, string action, object metadata, CancellationToken ct)
    {
        if (exercise.OrganizationId is Guid org)
            await _audit.RecordAuditEventAsync(actor.UserId, action, nameof(Exercise), exercise.Id, org, metadata: metadata, ct: ct);
        else
            await _audit.RecordPlatformAuditEventAsync(actor.UserId, action, nameof(Exercise), exercise.Id, metadata: metadata, ct: ct);
    }

    public static ExerciseMediaDto ToDto(ExerciseMedia m) =>
        new(m.Id, m.Sequence, m.AltText, m.Caption, m.SourceAttribution, m.Width, m.Height, m.ContentType, m.IsCurrent, m.CreatedAt);

    private async Task<ExerciseDto> ToDtoAsync(Exercise x, bool canEdit, CancellationToken ct)
    {
        var ids = new[] { x.CreatedById, x.UpdatedById, x.ReviewedById }.OfType<Guid>().Distinct().ToList();
        var names = await _db.Users.Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);
        string? Name(Guid? id) => id is Guid g ? names.GetValueOrDefault(g) : null;
        var images = x.Media.Where(m => m.IsCurrent).OrderBy(m => m.Sequence).Select(ToDto).ToList();
        return new ExerciseDto(x.Id, x.Code, x.Name, x.PatientDescription, x.ClinicalPurpose, x.BodyRegion, x.TargetMuscles, x.Category,
            x.StartingPosition, x.Instructions, x.EndingPosition, x.Equipment, x.Difficulty, x.Position, x.Laterality,
            x.BreathingInstructions, x.CommonMistakes, x.SafetyPrecautions, x.Contraindications, x.Progressions, x.Regressions, x.VideoUrl,
            x.IsActive, x.OrganizationId is null, x.NeedsClinicalReview, x.ReviewedAt, Name(x.ReviewedById), canEdit, images,
            x.CreatedAt, Name(x.CreatedById), x.UpdatedAt, Name(x.UpdatedById));
    }
}

using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Documents;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Media;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;
using SkiaSharp;

namespace PhysioTrac.Tests;

/// <summary>The HEP exercise library: search, clinic separation, platform
/// content, image upload safety, image sequences and history.</summary>
public class ExerciseLibraryTests
{
    /// <summary>Files kept in memory instead of on disk.</summary>
    public sealed class MemoryStorage : IFileStorage
    {
        public readonly ConcurrentDictionary<string, byte[]> Files = new();
        public async Task<string> SaveAsync(Stream content, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var key = Guid.NewGuid().ToString("N");
            Files[key] = ms.ToArray();
            return key;
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(Files[storageKey]));
        public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            Files.TryRemove(storageKey, out _);
            return Task.CompletedTask;
        }
    }

    private sealed record Ctx(PhysioTracDbContext Db, ExerciseLibraryService Library, MemoryStorage Storage, Organization Org, Organization OtherOrg,
        TestCurrentUser Therapist, TestCurrentUser Assistant, TestCurrentUser OtherClinic, TestCurrentUser Platform);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var other = new Organization { Name = "Other Clinic", Slug = "other" };
        db.Organizations.AddRange(org, other);
        await db.SaveChangesAsync();
        var audit = new AuditService(db);
        var storage = new MemoryStorage();
        var library = new ExerciseLibraryService(db, new TenantAccessService(db, audit), audit, storage, new SkiaExerciseImageProcessor());
        TestCurrentUser User(Organization o, UserRole role) => new() { UserId = TestTherapists.Add(db, o.Id, role), OrganizationId = o.Id, Role = role };
        return new Ctx(db, library, storage, org, other, User(org, UserRole.Therapist), User(org, UserRole.Assistant), User(other, UserRole.Therapist),
            new TestCurrentUser { UserId = Guid.NewGuid(), Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true });
    }

    private static SaveExerciseRequest Request(string name, ExerciseBodyRegion region = ExerciseBodyRegion.Knee, string? equipment = null,
        string? video = null) =>
        new(name, "Fictional description", null, region, "Quadriceps", ExerciseCategory.Strengthening, "Sit tall.", "Step one\nStep two", null,
            equipment, ExerciseDifficulty.Beginner, ExercisePosition.Sitting, ExerciseLaterality.EitherSide, null, null, null, null, null, null, video);

    /// <summary>A test image: an opaque gradient, or one with a transparent background.</summary>
    private static byte[] Image(int width, int height, SKEncodedImageFormat format, bool transparent = false)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(transparent ? SKColors.Transparent : SKColors.White);
            using var paint = new SKPaint { Color = SKColors.SteelBlue };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 90).ToArray();
    }

    private static UploadExerciseImage Upload(byte[] bytes, string name = "photo.png", string alt = "Person seated, knee bent", int? sequence = null,
        string? caption = null) =>
        new(new MemoryStream(bytes), bytes.Length, name, alt, caption, "Drawn by clinic staff", sequence);

    [Fact]
    public async Task TheStarterLibrary_IsPlatformContent_MarkedForReview_AndSeedsOnce()
    {
        var c = await SetupAsync();
        await ExerciseLibrarySeeder.SeedAsync(c.Db);
        await ExerciseLibrarySeeder.SeedAsync(c.Db);
        var all = c.Db.Exercises.ToList();
        Assert.Equal(ExerciseLibrarySeeder.Count, all.Count);
        Assert.True(all.Count >= 40);
        Assert.All(all, x => Assert.True(x.OrganizationId is null && x.NeedsClinicalReview && !string.IsNullOrWhiteSpace(x.Instructions)));
        Assert.Empty(c.Db.ExerciseMedia); // no images until approved ones are uploaded
        Assert.Equal(all.Count, all.Select(x => x.Code).Distinct().Count());
    }

    [Fact]
    public async Task Search_FindsByNameMuscleAndEquipment_AndFilters()
    {
        var c = await SetupAsync();
        await ExerciseLibrarySeeder.SeedAsync(c.Db);
        var bands = await c.Library.SearchAsync(new ExerciseSearch(Equipment: "band"), c.Therapist);
        Assert.Contains(bands, x => x.Code == "band-row");
        Assert.All(bands, x => Assert.Contains("band", x.Equipment!, StringComparison.OrdinalIgnoreCase));

        Assert.Contains(await c.Library.SearchAsync(new ExerciseSearch("gluteus medius"), c.Therapist), x => x.Code == "clamshell");
        var knee = await c.Library.SearchAsync(new ExerciseSearch(BodyRegion: ExerciseBodyRegion.Knee, Difficulty: ExerciseDifficulty.Beginner), c.Therapist);
        Assert.NotEmpty(knee);
        Assert.All(knee, x => Assert.Equal((ExerciseBodyRegion.Knee, ExerciseDifficulty.Beginner), (x.BodyRegion, x.Difficulty)));
        Assert.All(await c.Library.SearchAsync(new ExerciseSearch(Category: ExerciseCategory.BalanceCoordination), c.Therapist),
            x => Assert.Equal(ExerciseCategory.BalanceCoordination, x.Category));
    }

    [Fact]
    public async Task ClinicExercises_AreSeenAndChangedOnlyByThatClinic()
    {
        var c = await SetupAsync();
        var mine = await c.Library.CreateAsync(Request("Fictional knee drill"), c.Therapist);
        Assert.False(mine.IsPlatform);

        Assert.DoesNotContain(await c.Library.SearchAsync(new ExerciseSearch("Fictional"), c.OtherClinic), x => x.Id == mine.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => c.Library.GetAsync(mine.Id, c.OtherClinic));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Library.UpdateAsync(mine.Id, Request("Hijack"), c.OtherClinic));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Library.AddImageAsync(mine.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png)), c.OtherClinic));

        // An assistant can view but not change the library.
        await c.Library.GetAsync(mine.Id, c.Assistant);
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.CreateAsync(Request("Nope"), c.Assistant));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.UpdateAsync(mine.Id, Request("Nope"), c.Assistant));
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Scheduler };
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.SearchAsync(new ExerciseSearch(), scheduler));
    }

    [Fact]
    public async Task PlatformExercises_AreSharedButOnlyThePlatformChangesThem()
    {
        var c = await SetupAsync();
        var shared = await c.Library.CreateAsync(Request("Fictional platform exercise"), c.Platform);
        Assert.True(shared.IsPlatform);

        var seenByClinic = await c.Library.GetAsync(shared.Id, c.Therapist);
        Assert.False(seenByClinic.CanEdit);
        Assert.Contains(await c.Library.SearchAsync(new ExerciseSearch("platform"), c.OtherClinic), x => x.Id == shared.Id);

        var admin = new TestCurrentUser { UserId = TestTherapists.Add(c.Db, c.Org.Id, UserRole.Admin), OrganizationId = c.Org.Id, Role = UserRole.Admin };
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.UpdateAsync(shared.Id, Request("Changed"), admin));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.SetActiveAsync(shared.Id, false, admin));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.AddImageAsync(shared.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png)), admin));

        var image = await c.Library.AddImageAsync(shared.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png)), c.Platform);
        var (stream, _) = await c.Library.OpenImageAsync(shared.Id, image.Id, false, c.OtherClinic); // every clinic can see platform images
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task Validation_RejectsBadInput()
    {
        var c = await SetupAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Library.CreateAsync(Request(" "), c.Therapist));
        var bad = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Library.CreateAsync(Request("X", video: "javascript:alert(1)"), c.Therapist));
        Assert.Contains("https://", bad.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Library.CreateAsync(Request("X", video: "http://example.com/v"), c.Therapist));
        Assert.Equal("https://example.com/v", (await c.Library.CreateAsync(Request("X", video: "https://example.com/v"), c.Therapist)).VideoUrl);
    }

    [Fact]
    public async Task Uploads_AreCheckedByContent_AndReencoded()
    {
        var c = await SetupAsync();
        var x = await c.Library.CreateAsync(Request("Fictional"), c.Therapist);

        var png = await c.Library.AddImageAsync(x.Id, Upload(Image(2400, 1200, SKEncodedImageFormat.Png, transparent: true)), c.Therapist);
        Assert.Equal(("image/png", 1600, 800), (png.ContentType, png.Width, png.Height)); // scaled to the display size
        var jpeg = await c.Library.AddImageAsync(x.Id, Upload(Image(800, 600, SKEncodedImageFormat.Jpeg), "photo.jpg"), c.Therapist);
        Assert.Equal("image/jpeg", jpeg.ContentType);
        var webp = await c.Library.AddImageAsync(x.Id, Upload(Image(800, 600, SKEncodedImageFormat.Webp), "photo.webp"), c.Therapist);
        Assert.Equal("image/jpeg", webp.ContentType); // opaque WebP re-encoded as JPEG

        // The thumbnail really is smaller.
        var stored = c.Db.ExerciseMedia.Single(m => m.Id == png.Id);
        Assert.True(c.Storage.Files[stored.ThumbnailStorageKey].Length < c.Storage.Files[stored.StorageKey].Length);

        // Not an image, whatever the name says; GIF; too small; too big.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Library.AddImageAsync(x.Id, Upload(Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "evil.png"), c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Library.AddImageAsync(x.Id, Upload(Encoding.ASCII.GetBytes("GIF89a").Concat(new byte[200]).ToArray(), "a.gif"), c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Library.AddImageAsync(x.Id, Upload(Image(50, 50, SKEncodedImageFormat.Png)), c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Library.AddImageAsync(x.Id, new UploadExerciseImage(new MemoryStream(new byte[16]), ExerciseRules.MaxImageBytes + 1, "big.png", "alt", null, null), c.Therapist));
        // A file that claims to be small but isn't.
        var huge = new byte[ExerciseRules.MaxImageBytes + 10];
        Image(400, 300, SKEncodedImageFormat.Png).CopyTo(huge, 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Library.AddImageAsync(x.Id, new UploadExerciseImage(new MemoryStream(huge), 1000, "liar.png", "alt", null, null), c.Therapist));
        // Alternative text is required.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Library.AddImageAsync(x.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png), alt: " "), c.Therapist));
        Assert.Equal(3, c.Db.ExerciseMedia.Count());
    }

    [Fact]
    public async Task HiddenDataAfterTheImage_IsNotStored()
    {
        var c = await SetupAsync();
        var x = await c.Library.CreateAsync(Request("Fictional"), c.Therapist);
        var marker = Encoding.ASCII.GetBytes("HIDDEN-PAYLOAD-0123456789");
        var tampered = Image(400, 300, SKEncodedImageFormat.Png).Concat(marker).ToArray();
        var media = await c.Library.AddImageAsync(x.Id, Upload(tampered, "../../etc/passwd.png"), c.Therapist);

        var stored = c.Db.ExerciseMedia.Single(m => m.Id == media.Id);
        var bytes = c.Storage.Files[stored.StorageKey];
        Assert.DoesNotContain("HIDDEN-PAYLOAD", Encoding.ASCII.GetString(bytes));
        Assert.Equal("passwd.png", stored.OriginalFileName); // path removed, never used to store the file
        Assert.DoesNotContain("passwd", stored.StorageKey);
    }

    [Fact]
    public async Task ImageSequence_InsertReorderReplaceRemove_KeepsHistory()
    {
        var c = await SetupAsync();
        var x = await c.Library.CreateAsync(Request("Fictional"), c.Therapist);
        var start = await c.Library.AddImageAsync(x.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png), caption: "Starting position"), c.Therapist);
        var end = await c.Library.AddImageAsync(x.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png), caption: "Ending position"), c.Therapist);
        var middle = await c.Library.AddImageAsync(x.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png), caption: "Movement", sequence: 2), c.Therapist);

        async Task<string[]> Order() => (await c.Library.GetAsync(x.Id, c.Therapist)).Images.Select(i => i.Caption!).ToArray();
        Assert.Equal(["Starting position", "Movement", "Ending position"], await Order());

        await c.Library.ReorderImagesAsync(x.Id, [end.Id, middle.Id, start.Id], c.Therapist);
        Assert.Equal(["Ending position", "Movement", "Starting position"], await Order());
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Library.ReorderImagesAsync(x.Id, [end.Id, middle.Id], c.Therapist));

        // Replacing keeps the step; the old image is retired, not deleted.
        var replacement = await c.Library.ReplaceImageAsync(x.Id, middle.Id, Upload(Image(400, 300, SKEncodedImageFormat.Png), caption: "Movement v2"), c.Therapist);
        Assert.Equal(2, replacement.Sequence);
        var retired = c.Db.ExerciseMedia.Single(m => m.Id == middle.Id);
        Assert.Equal((false, replacement.Id), (retired.IsCurrent, retired.ReplacedByMediaId!.Value));
        var (old, _) = await c.Library.OpenImageAsync(x.Id, middle.Id, false, c.Therapist); // still viewable for programs that used it
        Assert.True(old.Length > 0);

        await c.Library.RemoveImageAsync(x.Id, end.Id, c.Therapist);
        var images = (await c.Library.GetAsync(x.Id, c.Therapist)).Images;
        Assert.Equal([1, 2], images.Select(i => i.Sequence));
        Assert.Equal(["Movement v2", "Starting position"], images.Select(i => i.Caption!));

        // Every image action is audited, without any text.
        var actions = c.Db.AuditEvents.Where(e => e.ObjectId == x.Id).Select(e => e.Action).ToList();
        Assert.Contains("exercise_media.uploaded", actions);
        Assert.Contains("exercise_media.reordered", actions);
        Assert.Contains("exercise_media.replaced", actions);
        Assert.Contains("exercise_media.removed", actions);
        Assert.DoesNotContain(c.Db.AuditEvents.ToList(), e => e.MetadataJson.Contains("Starting position"));
    }

    [Fact]
    public async Task MarkingReviewed_ClearsTheReviewFlag()
    {
        var c = await SetupAsync();
        await ExerciseLibrarySeeder.SeedAsync(c.Db);
        var mine = await c.Library.CreateAsync(Request("Fictional"), c.Therapist);
        var reviewed = await c.Library.MarkReviewedAsync(mine.Id, c.Therapist);
        Assert.Equal((false, c.Therapist.UserId), (reviewed.NeedsClinicalReview, c.Db.Exercises.Single(e => e.Id == mine.Id).ReviewedById!.Value));

        // A platform exercise's review belongs to the platform.
        var seeded = c.Db.Exercises.First(e => e.OrganizationId == null);
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Library.MarkReviewedAsync(seeded.Id, c.Therapist));
    }

    [Fact]
    public void SafeFileName_StripsPathsAndOddCharacters()
    {
        Assert.Equal("passwd", ExerciseLibraryService.SafeFileName("../../etc/passwd"));
        Assert.Equal("evil_name_.png", ExerciseLibraryService.SafeFileName(@"C:\x\evil<name>.png"));
        Assert.Null(ExerciseLibraryService.SafeFileName(".."));
    }
}

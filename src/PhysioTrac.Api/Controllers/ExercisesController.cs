using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>The Home Exercise Program exercise library and its images.
/// Images are served only through <see cref="Image"/>, after the same
/// access check as the exercise -- never from a public URL.</summary>
[ApiController]
[Route("api/v1/exercises")]
[Authorize]
public class ExercisesController : ControllerBase
{
    /// <summary>The image limit plus room for the form fields.</summary>
    private const long MaxRequestBodyBytes = ExerciseRules.MaxImageBytes + 64 * 1024;

    private readonly ICurrentUser _currentUser;
    private readonly IExerciseLibraryService _library;

    public ExercisesController(ICurrentUser currentUser, IExerciseLibraryService library)
    {
        _currentUser = currentUser;
        _library = library;
    }

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] string? q = null, [FromQuery] ExerciseBodyRegion? bodyRegion = null,
        [FromQuery] ExerciseCategory? category = null, [FromQuery] string? equipment = null, [FromQuery] ExerciseDifficulty? difficulty = null,
        [FromQuery] ExercisePosition? position = null, [FromQuery] bool includeInactive = false) =>
        Run(async ct => Ok(await _library.SearchAsync(
            new ExerciseSearch(q, bodyRegion, category, equipment, difficulty, position, includeInactive), _currentUser, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id) => Run(async ct => Ok(await _library.GetAsync(id, _currentUser, ct)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] SaveExerciseRequest request) =>
        Run(async ct =>
        {
            var created = await _library.CreateAsync(request, _currentUser, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveExerciseRequest request) =>
        Run(async ct => Ok(await _library.UpdateAsync(id, request, _currentUser, ct)));

    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id) => Run(async ct => Ok(await _library.SetActiveAsync(id, true, _currentUser, ct)));

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id) => Run(async ct => Ok(await _library.SetActiveAsync(id, false, _currentUser, ct)));

    /// <summary>A clinician confirms the exercise's written content.</summary>
    [HttpPost("{id:guid}/review")]
    public Task<IActionResult> Review(Guid id) => Run(async ct => Ok(await _library.MarkReviewedAsync(id, _currentUser, ct)));

    [HttpPost("{id:guid}/media")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public Task<IActionResult> AddImage(Guid id, [FromForm] ExerciseImageForm form) =>
        Run(async ct =>
        {
            if (form.File is null || form.File.Length == 0) return UnprocessableEntity(new { detail = "Choose an image to upload." });
            await using var stream = form.File.OpenReadStream();
            var media = await _library.AddImageAsync(id,
                new UploadExerciseImage(stream, form.File.Length, form.File.FileName, form.AltText ?? "", form.Caption, form.SourceAttribution, form.Sequence),
                _currentUser, ct);
            return StatusCode(201, media);
        });

    [HttpPost("{id:guid}/media/{mediaId:guid}/replace")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public Task<IActionResult> ReplaceImage(Guid id, Guid mediaId, [FromForm] ExerciseImageForm form) =>
        Run(async ct =>
        {
            if (form.File is null || form.File.Length == 0) return UnprocessableEntity(new { detail = "Choose an image to upload." });
            await using var stream = form.File.OpenReadStream();
            return Ok(await _library.ReplaceImageAsync(id, mediaId,
                new UploadExerciseImage(stream, form.File.Length, form.File.FileName, form.AltText ?? "", form.Caption, form.SourceAttribution),
                _currentUser, ct));
        });

    [HttpPut("{id:guid}/media/{mediaId:guid}")]
    public Task<IActionResult> UpdateImage(Guid id, Guid mediaId, [FromBody] UpdateExerciseImageRequest request) =>
        Run(async ct => Ok(await _library.UpdateImageAsync(id, mediaId, request, _currentUser, ct)));

    [HttpDelete("{id:guid}/media/{mediaId:guid}")]
    public Task<IActionResult> RemoveImage(Guid id, Guid mediaId) =>
        Run(async ct =>
        {
            await _library.RemoveImageAsync(id, mediaId, _currentUser, ct);
            return NoContent();
        });

    [HttpPut("{id:guid}/media/order")]
    public Task<IActionResult> ReorderImages(Guid id, [FromBody] ReorderExerciseImagesRequest request) =>
        Run(async ct => Ok(await _library.ReorderImagesAsync(id, request.MediaIds ?? [], _currentUser, ct)));

    /// <summary>The image itself (size=thumb for the thumbnail). Images never
    /// change once stored, so the browser may keep them -- privately.</summary>
    [HttpGet("{id:guid}/media/{mediaId:guid}")]
    public Task<IActionResult> Image(Guid id, Guid mediaId, [FromQuery] string? size = null) =>
        Run(async ct =>
        {
            var (content, contentType) = await _library.OpenImageAsync(id, mediaId, size == "thumb", _currentUser, ct);
            Response.Headers.CacheControl = "private, max-age=86400, immutable";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            // An <img> load (no Origin) and a script fetch (with Origin) must not
            // share a cached copy, or the script one fails its cross-origin check.
            Response.Headers.Vary = "Origin";
            return File(content, contentType);
        });

    private async Task<IActionResult> Run(Func<CancellationToken, Task<IActionResult>> action)
    {
        try
        {
            return await action(HttpContext.RequestAborted);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}

public class ExerciseImageForm
{
    public IFormFile? File { get; set; }
    public string? AltText { get; set; }
    public string? Caption { get; set; }
    public string? SourceAttribution { get; set; }
    /// <summary>Insert at this step (1 = first); default: last.</summary>
    public int? Sequence { get; set; }
}

public record ReorderExerciseImagesRequest(IReadOnlyList<Guid>? MediaIds);

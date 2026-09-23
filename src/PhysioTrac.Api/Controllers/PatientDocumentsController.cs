using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Documents;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patients/{patientId:guid}/documents")]
[Authorize]
public class PatientDocumentsController : ControllerBase
{
    private const long MaxRequestBodyBytes = 30 * 1024 * 1024;

    private readonly ICurrentUser _currentUser;
    private readonly IDocumentService _documents;

    public PatientDocumentsController(ICurrentUser currentUser, IDocumentService documents)
    {
        _currentUser = currentUser;
        _documents = documents;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId)
    {
        try
        {
            var documents = await _documents.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(documents.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Upload(Guid patientId, [FromForm] UploadDocumentForm form)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return UnprocessableEntity(new { detail = "A file is required." });
        }

        try
        {
            await using var stream = form.File.OpenReadStream();
            var request = new UploadDocumentRequest(
                patientId, form.Category, form.File.FileName, form.File.ContentType,
                form.File.Length, form.Description, stream);
            var document = await _documents.UploadAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), new { patientId }, ToDto(document));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid patientId, Guid documentId)
    {
        try
        {
            var (document, content) = await _documents.DownloadAsync(documentId, _currentUser, HttpContext.RequestAborted);
            if (document.PatientId != patientId)
            {
                await content.DisposeAsync();
                return NotFound(new { detail = "Document was not found." });
            }
            return File(content, document.ContentType, document.OriginalFilename);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid patientId, Guid documentId)
    {
        try
        {
            await _documents.DeleteAsync(documentId, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static DocumentDto ToDto(PatientDocument d) => new(
        d.Id, d.PatientId, d.Category, d.OriginalFilename, d.ContentType, d.FileSizeBytes,
        d.Description, d.UploadedById, d.CreatedAt);
}

public class UploadDocumentForm
{
    public IFormFile? File { get; set; }
    public PhysioTrac.Domain.Enums.DocumentCategory Category { get; set; } = PhysioTrac.Domain.Enums.DocumentCategory.Other;
    public string? Description { get; set; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Documents;

namespace PhysioTrac.Api.Controllers;

/// <summary>The unauthenticated half of secure document sharing --
/// deliberately carries no [Authorize] and no ICurrentUser: the token in the
/// URL, validated by IDocumentService.DownloadViaShareLinkAsync against
/// DocumentShareLink.IsUsable, is the entire access control. See
/// DocumentShareLink's own doc comment for why this is safe (32 random
/// bytes, only the hash ever persisted) and PatientDocumentsController for
/// where a share link is created/revoked (both staff-only, authenticated
/// actions).</summary>
[ApiController]
[Route("api/v1/shared-documents")]
[AllowAnonymous]
public class SharedDocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public SharedDocumentsController(IDocumentService documents)
    {
        _documents = documents;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Download(string token)
    {
        try
        {
            var (document, content) = await _documents.DownloadViaShareLinkAsync(token, HttpContext.RequestAborted);
            return File(content, document.ContentType, document.OriginalFilename);
        }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }
}

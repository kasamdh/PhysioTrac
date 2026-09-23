using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Documents;

namespace PhysioTrac.Web.Endpoints;

/// <summary>Plain (non-interactive) HTTP endpoint for downloading a patient
/// document. A Blazor Server component runs over a persistent SignalR
/// connection and can't stream a file response or set download headers
/// itself -- the Documents section on PatientDetail.razor links here
/// instead, matching AccountEndpoints.cs's same reasoning for login/logout.</summary>
public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        app.MapGet("/documents/{documentId:guid}/download", async (
            Guid documentId, ICurrentUser currentUser, IDocumentService documents, HttpContext http) =>
        {
            try
            {
                var (document, content) = await documents.DownloadAsync(documentId, currentUser, http.RequestAborted);
                return Results.File(content, document.ContentType, document.OriginalFilename);
            }
            catch (ForbiddenException)
            {
                return Results.Forbid();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();
    }
}

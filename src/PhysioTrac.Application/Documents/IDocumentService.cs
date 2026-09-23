using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Documents;

/// <summary>Patient file upload/list/download/delete, gated the same way
/// every other patient-scoped surface in this app is: through
/// ITenantAccessService.RequirePatientAccessAsync, so a staff member who
/// can't see a patient's chart can't see their documents either.</summary>
public interface IDocumentService
{
    Task<PatientDocument> UploadAsync(UploadDocumentRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<PatientDocument>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Returns the document's metadata and an open, readable stream
    /// of its content. Caller is responsible for disposing the stream.</summary>
    Task<(PatientDocument Document, Stream Content)> DownloadAsync(Guid documentId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Soft-delete only -- see PatientDocument.DeletedAt. The
    /// underlying file is left in storage; nothing here ever hard-deletes a
    /// document a clinic once had.</summary>
    Task DeleteAsync(Guid documentId, ICurrentUser actor, CancellationToken ct = default);
}

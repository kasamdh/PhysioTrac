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

    /// <summary>Creates a time-limited, unauthenticated share link -- see
    /// DocumentShareLink's own doc comment. Returns the raw token alongside
    /// the persisted row; the raw value is never recoverable again once this
    /// call returns (only its hash is stored).</summary>
    Task<(DocumentShareLink Link, string RawToken)> CreateShareLinkAsync(Guid documentId, int expiresInHours, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentShareLink>> ListShareLinksAsync(Guid documentId, ICurrentUser actor, CancellationToken ct = default);

    Task RevokeShareLinkAsync(Guid shareLinkId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The unauthenticated download path -- no ICurrentUser, since
    /// SharedDocumentsController carries no [Authorize]. The token itself,
    /// validated against DocumentShareLink.IsUsable, is the entire access
    /// control. Throws NotFoundException for any unusable token (unknown,
    /// expired, or revoked) -- deliberately the same outcome for all three,
    /// so a prober can't distinguish "wrong token" from "token expired."</summary>
    Task<(PatientDocument Document, Stream Content)> DownloadViaShareLinkAsync(string rawToken, CancellationToken ct = default);
}

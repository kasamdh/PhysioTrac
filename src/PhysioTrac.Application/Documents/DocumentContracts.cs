using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Documents;

public record DocumentDto(
    Guid Id, Guid PatientId, DocumentCategory Category, string OriginalFilename,
    string ContentType, long FileSizeBytes, string? Description,
    Guid UploadedById, DateTimeOffset CreatedAt);

public record UploadDocumentRequest(
    Guid PatientId, DocumentCategory Category, string OriginalFilename,
    string ContentType, long FileSizeBytes, string? Description, Stream Content);

public record DocumentShareLinkDto(
    Guid Id, Guid PatientDocumentId, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt,
    int AccessCount, DateTimeOffset? LastAccessedAt);

/// <summary>Returned only once, from the create-link call -- Token is the
/// raw, one-time-visible value the caller must copy immediately; it's never
/// retrievable again (only its hash is persisted).</summary>
public record CreatedDocumentShareLinkDto(DocumentShareLinkDto Link, string Token);

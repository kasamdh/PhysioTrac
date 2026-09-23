using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Documents;

public record DocumentDto(
    Guid Id, Guid PatientId, DocumentCategory Category, string OriginalFilename,
    string ContentType, long FileSizeBytes, string? Description,
    Guid UploadedById, DateTimeOffset CreatedAt);

public record UploadDocumentRequest(
    Guid PatientId, DocumentCategory Category, string OriginalFilename,
    string ContentType, long FileSizeBytes, string? Description, Stream Content);

using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Messaging;

public record MessageDto(
    Guid Id, Guid PatientId, Guid SenderId, UserRole SenderRole, bool IsFromPatient,
    string Body, DateTimeOffset SentAt, DateTimeOffset? ReadAt);

public record SendMessageRequest(Guid PatientId, string Body);

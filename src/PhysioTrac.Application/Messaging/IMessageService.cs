using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Messaging;

/// <summary>Secure messaging placeholder -- see Message's own doc comment.
/// Every send/read today is staff-initiated; there is no patient-portal
/// login yet for a patient to author their own side of the thread.</summary>
public interface IMessageService
{
    Task<Message> SendAsync(SendMessageRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<Message>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Marks every unread message in the thread as read by the
    /// caller -- a coarse "mark thread read", not per-message.</summary>
    Task MarkThreadReadAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Inbox view: one row per patient (within the caller's patient
    /// scope) who has at least one message, most recent thread first.</summary>
    Task<IReadOnlyList<MessageThreadSummary>> ListThreadsAsync(ICurrentUser actor, CancellationToken ct = default);
}

public record MessageThreadSummary(
    Guid PatientId, string PatientName, string MedicalRecordNumber,
    DateTimeOffset LastMessageAt, string LastMessagePreview, int MessageCount, int UnreadCount);

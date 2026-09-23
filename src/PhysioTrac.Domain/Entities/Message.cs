using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One message in a patient's secure-messaging thread. A genuine
/// "placeholder" per the spec: no patient portal login exists yet, so every
/// message today is staff-authored (IsFromPatient is always false in
/// practice) -- the shape is ready for real patient-authored messages the
/// day portal auth exists, without a schema change.</summary>
public class Message : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid SenderId { get; set; }

    /// <summary>Denormalized at send time -- who a sender "was" (their role)
    /// stays correct in the thread's history even if that user's role
    /// changes or their account is later archived.</summary>
    public UserRole SenderRole { get; set; }

    public bool IsFromPatient { get; set; }

    public string Body { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public Guid? ReadById { get; set; }
}

using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Append-only record of every status an appointment has moved
/// through. Written automatically by AppointmentService on every status
/// change (including the initial Scheduled row at creation) -- never
/// user-editable directly.</summary>
public class AppointmentStatusHistory : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public AppointmentStatus? FromStatus { get; set; }
    public AppointmentStatus ToStatus { get; set; }
    public Guid ChangedById { get; set; }
    public string? Reason { get; set; }
}

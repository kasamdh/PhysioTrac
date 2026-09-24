using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Clinic-administrator-configured appointment type label and
/// default duration. Deliberately separate from <see cref="Appointment.Kind"/>
/// (the fixed clinical-workflow enum) — this is the org-facing scheduling
/// label a clinic administrator maintains.</summary>
public class AppointmentType : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultDurationMinutes { get; set; } = 30;
    public decimal? Price { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public bool OnlineBookingEnabled { get; set; } = true;

    /// <summary>Only offered to patients booking as a new patient (e.g. Initial Evaluation).</summary>
    public bool RequiresNewPatient { get; set; }

    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }

    /// <summary>Optional clinical-workflow default this maps to.</summary>
    public AppointmentKind? DefaultKind { get; set; }

    /// <summary>The flat CPT code ChargeService.GenerateFromAppointmentAsync
    /// bills a completed appointment of this type under (paired with
    /// <see cref="Price"/>, or the fee schedule if Price is unset) -- the
    /// simple per-visit billing path for a cash-pay clinic that doesn't
    /// itemize by treatment, as opposed to GenerateFromNoteAsync's per-
    /// intervention itemization. Null means this appointment type can't be
    /// billed this way; a biller must generate its charges from the note
    /// instead (or enter one manually).</summary>
    public string? DefaultCptCode { get; set; }
}

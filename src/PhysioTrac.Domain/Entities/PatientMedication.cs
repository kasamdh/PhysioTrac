using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>One medication on a patient's active/historical med list.
/// Discontinuing a medication sets IsActive = false rather than deleting
/// the row -- a PT plan of care may need to know what a patient WAS taking,
/// not just what they take now.</summary>
public class PatientMedication : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? PrescribingProvider { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DiscontinuedDate { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid RecordedById { get; set; }
}

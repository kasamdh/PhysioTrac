using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Links a patient's chart to a structured ICD-10-CM code
/// (DiagnosisCode is shared, read-only reference data -- see its own doc
/// comment). This is additive to the existing free-text Patient.Diagnoses
/// field, not a replacement: the free-text field is still shown as a quick
/// chart-header summary, while this collection is the structured billing/
/// reporting-grade diagnosis list.</summary>
public class PatientDiagnosis : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid DiagnosisCodeId { get; set; }
    public DiagnosisCode? DiagnosisCode { get; set; }

    public bool IsPrimary { get; set; }
    public DateOnly? DiagnosedDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public string? Notes { get; set; }

    public bool IsResolved => ResolvedDate is not null;
}

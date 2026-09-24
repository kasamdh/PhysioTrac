using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A patient's filled-out response to an <see cref="IntakeFormTemplate"/>,
/// almost always submitted through the patient portal (see
/// IIntakeFormService.SubmitAsync). Snapshots the template's version at
/// submission time, the same reasoning ClinicalNoteVersion/Consent.TemplateVersion
/// already apply -- a later change to the form definition never rewrites
/// what a patient actually saw and answered.</summary>
public class IntakeFormSubmission : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid IntakeFormTemplateId { get; set; }
    public IntakeFormTemplate? IntakeFormTemplate { get; set; }

    public int TemplateVersion { get; set; }

    /// <summary>The patient's answers -- one JSON blob per submission,
    /// matching the same "never queried across rows in SQL" precedent
    /// ClinicalNote's *Json fields already establish.</summary>
    public string ResponseJson { get; set; } = "{}";

    public IntakeFormSubmissionStatus Status { get; set; } = IntakeFormSubmissionStatus.Submitted;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

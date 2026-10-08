using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>The facts shown at the top of a clinical encounter.</summary>
public record EncounterHeaderDto(
    Guid PatientId, string PatientName, DateOnly DateOfBirth, int Age, string MedicalRecordNumber,
    Guid? AppointmentId, DateTimeOffset? AppointmentStartsAt, DateTimeOffset? AppointmentEndsAt,
    int VisitNumber, string VisitType, string AuthorName, string? TreatingProviderName, string? SupervisingProviderName,
    string? ReferringProviderName,
    IReadOnlyList<string> Diagnoses, IReadOnlyList<string> Allergies, string? Precautions,
    Guid? ActivePlanOfCareId, DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd);

/// <summary>Everything an encounter workspace needs to open a note: the
/// note, the exact template version it was written with, its field values,
/// the header facts, and the save version to send back with the next save.</summary>
public record EncounterDto(
    ClinicalNoteDto Note,
    string? TemplateName,
    TemplateVersionDto? Template,
    IReadOnlyList<TemplateFieldValueDto> Values,
    EncounterHeaderDto Header,
    int SaveVersion,
    DateTimeOffset LastSavedAt,
    string? LastSavedByName);

/// <summary>An encounter save (autosave). <see cref="BaseSaveVersion"/> is
/// the SaveVersion the editor last loaded or saved; if someone else saved
/// in between, the save is refused (409) instead of overwriting them.
/// Narrative columns and JSON follow UpdateNoteRequest's rules: null =
/// unchanged. Values replace those keys; a value with nothing set clears it.</summary>
public record SaveEncounterRequest(
    int BaseSaveVersion,
    IReadOnlyList<TemplateFieldValueDto>? Values = null,
    string? Subjective = null, string? Objective = null, string? Interventions = null, string? Assessment = null, string? Plan = null,
    string? SubjectiveDetailsJson = null, string? ObjectiveMeasurementsJson = null);

public record EncounterSaveResultDto(int SaveVersion, DateTimeOffset SavedAt, string? SavedByName);

/// <summary>Switch a draft's template (only while it has no template values yet).</summary>
public record ChangeNoteTemplateRequest(Guid TemplateId);

/// <summary>Someone else saved the note since this editor loaded it.</summary>
public class EncounterConflictException : InvalidOperationException
{
    public int CurrentSaveVersion { get; }
    public DateTimeOffset SavedAt { get; }
    public string? SavedByName { get; }

    public EncounterConflictException(int currentSaveVersion, DateTimeOffset savedAt, string? savedByName)
        : base($"This note was changed{(savedByName is null ? "" : $" by {savedByName}")} after you opened it. Reload to see the latest version.")
    {
        CurrentSaveVersion = currentSaveVersion;
        SavedAt = savedAt;
        SavedByName = savedByName;
    }
}

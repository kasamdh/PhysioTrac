using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Intake;

/// <summary>Configurable digital intake forms -- the form-builder twin of
/// IClinicalTemplateService for form definitions, plus the patient-facing
/// submission half neither clinical notes nor consents need (a patient
/// fills these out themselves, almost always through the portal).</summary>
public interface IIntakeFormService
{
    Task<IntakeFormTemplate> CreateTemplateAsync(CreateIntakeFormTemplateRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<IntakeFormTemplate>> ListTemplatesAsync(ICurrentUser actor, CancellationToken ct = default);

    Task<IntakeFormTemplate?> ResolveTemplateAsync(ICurrentUser actor, string key, Guid? locationId, string? state, CancellationToken ct = default);

    /// <summary>Every distinct form Key configured for this organization
    /// (Platform defaults included), each resolved to its current
    /// most-specific version -- "which forms does this patient need to
    /// fill out," for the portal to list.</summary>
    Task<IReadOnlyList<IntakeFormTemplate>> ListResolvedForOrganizationAsync(ICurrentUser actor, Guid? locationId, string? state, CancellationToken ct = default);

    /// <summary>Patient self-service submission via
    /// ITenantAccessService.RequirePortalPatientAsync -- never accepts a
    /// client-supplied patient id.</summary>
    Task<IntakeFormSubmission> SubmitAsync(ICurrentUser patientUser, SubmitIntakeFormRequest request, CancellationToken ct = default);

    /// <summary>Readable by the submitting patient (portal) or any staff
    /// role with chart access to this patient -- same
    /// RequirePatientAccessAsync-only pattern as documents/HEP/consents.</summary>
    Task<IReadOnlyList<IntakeFormSubmission>> ListSubmissionsForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    Task<IntakeFormSubmission> ReviewAsync(Guid submissionId, ICurrentUser actor, CancellationToken ct = default);
}

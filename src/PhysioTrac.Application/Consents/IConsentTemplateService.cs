using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Consents;

/// <summary>Configurable consent-language templates -- the consent-side twin
/// of IClinicalTemplateService, same scoping/versioning/resolution rules.</summary>
public interface IConsentTemplateService
{
    Task<ConsentTemplate> CreateAsync(CreateConsentTemplateRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<ConsentTemplate>> ListAsync(ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Most-specific-wins: Location -> State -> Organization ->
    /// Platform default. Returns null only if even the Platform default is
    /// missing (shouldn't happen once the platform defaults are seeded).</summary>
    Task<ConsentTemplate?> ResolveAsync(ICurrentUser actor, ConsentType consentType, Guid? locationId, string? state, CancellationToken ct = default);

    /// <summary>Same resolution as <see cref="ResolveAsync"/> but keyed
    /// directly by organization id -- used by ConsentService.RecordAsync,
    /// which already has the patient's organization resolved and no
    /// necessarily-staff ICurrentUser to authorize against (a patient
    /// signing their own consent still needs the current template
    /// resolved).</summary>
    Task<ConsentTemplate?> ResolveForOrganizationAsync(Guid organizationId, ConsentType consentType, Guid? locationId, string? state, CancellationToken ct = default);
}

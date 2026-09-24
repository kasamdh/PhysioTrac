using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Consents;

/// <summary>Records and lists e-signature consents. The consent text comes
/// from the current ConsentTemplate for the signer's organization/consent
/// type (falling back to the fixed ConsentTypeText constant if none has been
/// configured) and is snapshotted onto the row at signing time -- along with
/// which template version was current -- so it stays exactly what the
/// signer actually saw even if the clinic's language changes later.</summary>
public interface IConsentService
{
    /// <summary>Staff recording a consent on a patient's behalf (e.g. a
    /// paper form transcribed at check-in) -- requires RoleSets
    /// .DocumentManagement. See <see cref="RecordOwnAsync"/> for the
    /// patient-self-service path.</summary>
    Task<Consent> RecordAsync(RecordConsentRequest request, ICurrentUser actor, string? ipAddress, CancellationToken ct = default);

    /// <summary>A patient signing their own consent through the portal --
    /// resolves "which patient" via ITenantAccessService.RequirePortalPatientAsync,
    /// never from a client-supplied patient id, and carries no staff role
    /// requirement.</summary>
    Task<Consent> RecordOwnAsync(ICurrentUser patientUser, RecordOwnConsentRequest request, string? ipAddress, CancellationToken ct = default);

    Task<IReadOnlyList<Consent>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    Task<Consent> RevokeAsync(Guid consentId, RevokeConsentRequest request, ICurrentUser actor, CancellationToken ct = default);
}

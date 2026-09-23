using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Consents;

/// <summary>Records and lists e-signature consents. The consent text itself
/// comes from ConsentTypeText (a fixed constant per type today, not yet a
/// clinic-configurable template) and is snapshotted onto the row at signing
/// time, so it stays exactly what the signer actually saw even if the
/// clinic's language changes later.</summary>
public interface IConsentService
{
    Task<Consent> RecordAsync(RecordConsentRequest request, ICurrentUser actor, string? ipAddress, CancellationToken ct = default);

    Task<IReadOnlyList<Consent>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    Task<Consent> RevokeAsync(Guid consentId, RevokeConsentRequest request, ICurrentUser actor, CancellationToken ct = default);
}

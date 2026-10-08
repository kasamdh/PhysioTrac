using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Clinical;

/// <summary>Records, scores and lists outcome measures -- one score per
/// patient/measure/day. Item responses are scored by
/// <see cref="OutcomeMeasureCatalog"/>; a score attached to a signed note
/// can no longer be changed or removed.</summary>
public interface IOutcomeScoreService
{
    Task<OutcomeScore> RecordAsync(RecordOutcomeScoreRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Removes a score that isn't part of a signed note.</summary>
    Task DeleteAsync(Guid scoreId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The patient's scores as DTOs (responses, interpretation, and
    /// whether a signed note locks them), oldest first per measure.</summary>
    Task<IReadOnlyList<OutcomeScoreDto>> ListDtosForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<OutcomeScore>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);
}

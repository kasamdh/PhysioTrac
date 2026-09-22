using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Clinical;

/// <summary>Direct port of the `OutcomeScore` recording path from
/// `care/models.py` — one score per patient/measure/day. Deterministic trend
/// computation (`services.outcome_trends`) is deferred to a later module;
/// this only records and lists raw scores.</summary>
public interface IOutcomeScoreService
{
    Task<OutcomeScore> RecordAsync(RecordOutcomeScoreRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<OutcomeScore>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);
}

using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Clinical;

/// <summary>Direct port of the `FunctionalGoal` lifecycle from `care/models.py`
/// — a goal starts as Draft and requires a clinician's explicit approval
/// (Admin/Director/Therapist) before it becomes Active, matching the
/// model's own `clean()` invariant.</summary>
public interface IFunctionalGoalService
{
    Task<FunctionalGoal> CreateAsync(CreateGoalRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Approves a Draft goal, moving it to Active. Throws
    /// <see cref="Common.ForbiddenException"/> unless the caller can sign
    /// notes (Admin/Director/Therapist) and <see cref="Common.InvalidOperationException"/>-style
    /// domain error if required fields are missing.</summary>
    Task<FunctionalGoal> ApproveAsync(Guid goalId, ICurrentUser actor, CancellationToken ct = default);

    Task<FunctionalGoal> UpdateProgressAsync(Guid goalId, UpdateGoalProgressRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<FunctionalGoal>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);
}

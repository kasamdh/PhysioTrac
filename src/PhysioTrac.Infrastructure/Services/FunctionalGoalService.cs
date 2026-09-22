using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of the `FunctionalGoal` lifecycle in `care/models.py`.</summary>
public class FunctionalGoalService : IFunctionalGoalService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public FunctionalGoalService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<FunctionalGoal> CreateAsync(CreateGoalRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, ct: ct);

        var goal = new FunctionalGoal
        {
            PatientId = patient.Id,
            AuthorId = actor.UserId,
            FunctionalLimitation = request.FunctionalLimitation,
            FunctionalTask = request.FunctionalTask,
            BaselineValue = request.BaselineValue,
            TargetValue = request.TargetValue,
            Unit = request.Unit,
            MeasurementMethod = request.MeasurementMethod,
            TargetDate = request.TargetDate,
            SuggestedWording = request.SuggestedWording,
            Status = GoalStatus.Draft,
        };
        _db.FunctionalGoals.Add(goal);
        await _db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task<FunctionalGoal> ApproveAsync(Guid goalId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, new HashSet<UserRole> { UserRole.Admin, UserRole.Director, UserRole.Therapist });
        var goal = await LoadGoalInOrgAsync(goalId, actor, ct);

        if (string.IsNullOrWhiteSpace(goal.FunctionalLimitation) || string.IsNullOrWhiteSpace(goal.FunctionalTask)
            || string.IsNullOrWhiteSpace(goal.Unit) || string.IsNullOrWhiteSpace(goal.MeasurementMethod))
        {
            throw new InvalidOperationException("Required before a goal can become active.");
        }

        goal.Status = GoalStatus.Active;
        goal.ApprovedById = actor.UserId;
        goal.ApprovedAt = DateTimeOffset.UtcNow;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "goal.approved", nameof(FunctionalGoal), goal.Id, organization.Id,
            patientId: goal.PatientId, ct: ct);
        return goal;
    }

    public async Task<FunctionalGoal> UpdateProgressAsync(Guid goalId, UpdateGoalProgressRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var goal = await LoadGoalInOrgAsync(goalId, actor, ct);

        goal.CurrentValue = request.CurrentValue;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task<IReadOnlyList<FunctionalGoal>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.FunctionalGoals.Where(g => g.PatientId == patient.Id)
            .OrderBy(g => g.Status).ThenBy(g => g.TargetDate).ToListAsync(ct);
    }

    private async Task<FunctionalGoal> LoadGoalInOrgAsync(Guid goalId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var goal = await _db.FunctionalGoals.FirstOrDefaultAsync(g => g.Id == goalId, ct)
            ?? throw new NotFoundException("Goal was not found.");
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == goal.PatientId, ct);
        if (patient is null || patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Goal was not found.");
        }
        return goal;
    }
}

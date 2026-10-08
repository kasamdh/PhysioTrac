using System.Text.Json;
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

/// <summary>The functional goal lifecycle: a goal starts as Draft and needs a
/// clinician's approval (Admin/Director/Therapist) before it is tracked.
/// Every change is appended to <see cref="FunctionalGoalHistory"/>, so no
/// earlier version or progress entry is ever overwritten.</summary>
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
        RequireValid(request.FunctionalTask, request.Unit, request.BaselineValue, request.TargetValue);

        var goal = new FunctionalGoal
        {
            PatientId = patient.Id,
            AuthorId = actor.UserId,
            FunctionalLimitation = request.FunctionalLimitation.Trim(),
            FunctionalTask = request.FunctionalTask.Trim(),
            Term = request.Term,
            BaselineValue = request.BaselineValue,
            TargetValue = request.TargetValue,
            Unit = request.Unit.Trim(),
            MeasurementMethod = request.MeasurementMethod.Trim(),
            TargetDate = request.TargetDate,
            SuggestedWording = request.SuggestedWording,
            Comments = Clean(request.Comments),
            Status = GoalStatus.Draft,
        };
        _db.FunctionalGoals.Add(goal);
        _db.FunctionalGoalHistory.Add(History(goal, GoalHistoryKind.Created, actor.UserId));
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
        if (goal.Status != GoalStatus.Draft) throw new InvalidOperationException("Only a draft goal can be approved.");

        goal.Status = goal.CurrentValue is null ? GoalStatus.NotStarted : GoalStatus.Active;
        goal.ApprovedById = actor.UserId;
        goal.ApprovedAt = DateTimeOffset.UtcNow;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        _db.FunctionalGoalHistory.Add(History(goal, GoalHistoryKind.Approved, actor.UserId));
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "goal.approved", nameof(FunctionalGoal), goal.Id, organization.Id,
            patientId: goal.PatientId, ct: ct);
        return goal;
    }

    public async Task<FunctionalGoal> UpdateAsync(Guid goalId, UpdateGoalRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var goal = await LoadGoalInOrgAsync(goalId, actor, ct);
        RequireValid(request.FunctionalTask, request.Unit, request.BaselineValue, request.TargetValue);

        goal.FunctionalLimitation = request.FunctionalLimitation.Trim();
        goal.FunctionalTask = request.FunctionalTask.Trim();
        goal.Term = request.Term;
        goal.BaselineValue = request.BaselineValue;
        goal.TargetValue = request.TargetValue;
        goal.Unit = request.Unit.Trim();
        goal.MeasurementMethod = request.MeasurementMethod.Trim();
        goal.TargetDate = request.TargetDate;
        goal.Comments = Clean(request.Comments);
        goal.Version++;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        _db.FunctionalGoalHistory.Add(History(goal, GoalHistoryKind.Edited, actor.UserId));
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "goal.edited", nameof(FunctionalGoal), goal.Id, organization.Id,
            patientId: goal.PatientId, metadata: new { version = goal.Version }, ct: ct);
        return goal;
    }

    public async Task<FunctionalGoal> UpdateProgressAsync(Guid goalId, UpdateGoalProgressRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var goal = await LoadGoalInOrgAsync(goalId, actor, ct);
        if (goal.Status == GoalStatus.Draft) throw new InvalidOperationException("Approve the goal before recording progress.");
        if (request.Status is GoalStatus status && !GoalRules.IsRecordable(status))
            throw new InvalidOperationException("Choose Not started, In progress, Met, Partially met or Discontinued.");

        ApplyProgress(goal, request.CurrentValue, request.Status);
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        _db.FunctionalGoalHistory.Add(History(goal, GoalHistoryKind.Progress, actor.UserId, comment: Clean(request.Comment)));
        await _db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task<IReadOnlyList<GoalHistoryDto>> HistoryAsync(Guid goalId, ICurrentUser actor, CancellationToken ct = default)
    {
        var goal = await LoadGoalInOrgAsync(goalId, actor, ct);
        await _tenantAccess.RequirePatientAccessAsync(actor, goal.PatientId, ct: ct);
        var rows = await _db.FunctionalGoalHistory.AsNoTracking().Where(h => h.GoalId == goal.Id)
            .OrderBy(h => h.CreatedAt).ToListAsync(ct);
        var userIds = rows.Select(r => r.RecordedById).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var noteIds = rows.Where(r => r.NoteId != null).Select(r => r.NoteId!.Value).Distinct().ToList();
        var dates = await _db.ClinicalNotes.AsNoTracking().Where(n => noteIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.ServiceDate, ct);
        return rows.Select(h => new GoalHistoryDto(h.Id, h.Kind, h.GoalVersion, h.NoteId,
            h.NoteId is Guid n && dates.TryGetValue(n, out var d) ? d : null, h.RecordedById, names.GetValueOrDefault(h.RecordedById),
            h.CreatedAt, h.Status, h.CurrentValue, h.ProgressPercent, h.Comment,
            JsonSerializer.Deserialize<GoalSnapshotDto>(h.SnapshotJson)!)).ToList();
    }

    public async Task<IReadOnlyList<FunctionalGoal>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.FunctionalGoals.Where(g => g.PatientId == patient.Id)
            .OrderBy(g => g.Status).ThenBy(g => g.TargetDate).ToListAsync(ct);
    }

    /// <summary>Records a value and/or status. A first value moves a
    /// not-started goal to in progress unless a status is given.</summary>
    internal static void ApplyProgress(FunctionalGoal goal, decimal? value, GoalStatus? status)
    {
        if (value is decimal v) goal.CurrentValue = v;
        if (status is GoalStatus s) goal.Status = s;
        else if (value is not null && goal.Status == GoalStatus.NotStarted) goal.Status = GoalStatus.Active;
    }

    internal static GoalSnapshotDto Snapshot(FunctionalGoal g) => new(
        g.Term, g.FunctionalTask, g.FunctionalLimitation, g.BaselineValue, g.TargetValue, g.Unit, g.MeasurementMethod,
        g.TargetDate, g.Comments);

    internal static FunctionalGoalHistory History(FunctionalGoal goal, GoalHistoryKind kind, Guid actorId, Guid? noteId = null,
        string? comment = null) => new()
        {
            GoalId = goal.Id,
            Kind = kind,
            GoalVersion = goal.Version,
            NoteId = noteId,
            RecordedById = actorId,
            Status = goal.Status,
            CurrentValue = goal.CurrentValue,
            ProgressPercent = goal.ProgressPercent,
            Comment = comment,
            SnapshotJson = JsonSerializer.Serialize(Snapshot(goal)),
        };

    private static void RequireValid(string task, string unit, decimal baseline, decimal target)
    {
        var errors = GoalRules.Validate(task, unit, baseline, target);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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

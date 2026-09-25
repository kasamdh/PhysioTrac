using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of the `OutcomeScore` recording path in `care/models.py`.</summary>
public class OutcomeScoreService : IOutcomeScoreService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public OutcomeScoreService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<OutcomeScore> RecordAsync(RecordOutcomeScoreRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, ct: ct);

        if (request.MaximumScore is decimal max && request.Score > max)
        {
            throw new InvalidOperationException("Score cannot exceed the entered maximum.");
        }
        if (request.Score < 0)
        {
            throw new InvalidOperationException("Score cannot be negative.");
        }

        var existing = await _db.OutcomeScores.FirstOrDefaultAsync(
            o => o.PatientId == patient.Id && o.Measure == request.Measure && o.MeasuredOn == request.MeasuredOn, ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (existing is not null)
        {
            existing.Score = request.Score;
            existing.MaximumScore = request.MaximumScore;
            existing.Notes = request.Notes;
            existing.NoteId = request.NoteId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            // OutcomeScore has no OrganizationId of its own, so it isn't
            // covered by EntityChangeAuditInterceptor.
            await _audit.RecordAuditEventAsync(actor.UserId, "outcome_score.updated", nameof(OutcomeScore), existing.Id,
                organization.Id, patientId: patient.Id, metadata: new { measure = existing.Measure.ToString() }, ct: ct);
            return existing;
        }

        var score = new OutcomeScore
        {
            PatientId = patient.Id,
            NoteId = request.NoteId,
            RecordedById = actor.UserId,
            Measure = request.Measure,
            MeasuredOn = request.MeasuredOn,
            Score = request.Score,
            MaximumScore = request.MaximumScore,
            Notes = request.Notes,
        };
        _db.OutcomeScores.Add(score);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "outcome_score.recorded", nameof(OutcomeScore), score.Id,
            organization.Id, patientId: patient.Id, metadata: new { measure = score.Measure.ToString() }, ct: ct);
        return score;
    }

    public async Task<IReadOnlyList<OutcomeScore>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.OutcomeScores.Where(o => o.PatientId == patient.Id)
            .OrderBy(o => o.Measure).ThenBy(o => o.MeasuredOn).ToListAsync(ct);
    }
}

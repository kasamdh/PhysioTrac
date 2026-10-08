using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Records, scores and lists outcome measures (one score per
/// patient/measure/day). Item responses are scored by
/// <see cref="OutcomeMeasureCatalog"/>. A score recorded on a note belongs
/// to that note: once the note is signed it can't be changed or removed.</summary>
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
        if (!OutcomeMeasureCatalog.IsKnown(request.Measure)) throw new InvalidOperationException("Unknown outcome measure.");
        var definition = OutcomeMeasureCatalog.Get(request.Measure);

        decimal score;
        string responsesJson = "{}";
        if (request.ItemResponses is { Count: > 0 } responses)
        {
            var result = OutcomeMeasureCatalog.Score(request.Measure, responses);
            if (!result.IsValid) throw new InvalidOperationException(string.Join(" ", result.Errors));
            score = result.Score;
            var answered = responses.Where(r => r.Value is not null)
                .Select(r => r with { Label = definition.ItemsNamedByPatient ? r.Label?.Trim() : null }).ToList();
            responsesJson = JsonSerializer.Serialize(answered);
        }
        else
        {
            if (request.Score is not decimal total) throw new InvalidOperationException("Enter the item responses or the total score.");
            if (total < 0) throw new InvalidOperationException("Score cannot be negative.");
            var errors = OutcomeMeasureCatalog.ValidateTotal(request.Measure, total);
            if (errors.Count > 0) throw new InvalidOperationException(errors[0]);
            score = total;
        }

        if (request.NoteId is Guid noteId)
        {
            var note = await _db.ClinicalNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == noteId, ct);
            if (note is null || note.PatientId != patient.Id) throw new InvalidOperationException("The note is not this patient's.");
            if (!note.IsEditable) throw new InvalidOperationException("This note is signed; scores can no longer be added to it.");
        }

        var existing = await _db.OutcomeScores.FirstOrDefaultAsync(
            o => o.PatientId == patient.Id && o.Measure == request.Measure && o.MeasuredOn == request.MeasuredOn, ct);
        if (existing is not null && await IsLockedAsync(existing.NoteId, ct))
        {
            throw new InvalidOperationException(
                $"The {definition.Abbreviation} score for {request.MeasuredOn:MM/dd/yyyy} is part of a signed note and can't be changed.");
        }

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var interpretation = OutcomeMeasureCatalog.Interpret(request.Measure, score);

        if (existing is not null)
        {
            existing.Score = score;
            existing.MaximumScore = definition.ScoreMax;
            existing.Notes = Clean(request.Notes);
            existing.NoteId = request.NoteId;
            existing.ItemResponsesJson = responsesJson;
            existing.Interpretation = interpretation;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            // OutcomeScore has no OrganizationId of its own, so it isn't
            // covered by EntityChangeAuditInterceptor.
            await _audit.RecordAuditEventAsync(actor.UserId, "outcome_score.updated", nameof(OutcomeScore), existing.Id,
                organization.Id, patientId: patient.Id, metadata: new { measure = existing.Measure.ToString() }, ct: ct);
            return existing;
        }

        var row = new OutcomeScore
        {
            PatientId = patient.Id,
            NoteId = request.NoteId,
            RecordedById = actor.UserId,
            Measure = request.Measure,
            MeasuredOn = request.MeasuredOn,
            Score = score,
            MaximumScore = definition.ScoreMax,
            Notes = Clean(request.Notes),
            ItemResponsesJson = responsesJson,
            Interpretation = interpretation,
        };
        _db.OutcomeScores.Add(row);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "outcome_score.recorded", nameof(OutcomeScore), row.Id,
            organization.Id, patientId: patient.Id, metadata: new { measure = row.Measure.ToString() }, ct: ct);
        return row;
    }

    public async Task DeleteAsync(Guid scoreId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var score = await _db.OutcomeScores.FirstOrDefaultAsync(o => o.Id == scoreId, ct)
            ?? throw new NotFoundException("Score was not found.");
        await _tenantAccess.RequirePatientAccessAsync(actor, score.PatientId, ct: ct);
        if (await IsLockedAsync(score.NoteId, ct))
            throw new InvalidOperationException("This score is part of a signed note and can't be removed.");

        _db.OutcomeScores.Remove(score);
        await _db.SaveChangesAsync(ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "outcome_score.deleted", nameof(OutcomeScore), score.Id,
            organization.Id, patientId: score.PatientId, metadata: new { measure = score.Measure.ToString() }, ct: ct);
    }

    public async Task<IReadOnlyList<OutcomeScore>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.OutcomeScores.Where(o => o.PatientId == patient.Id)
            .OrderBy(o => o.Measure).ThenBy(o => o.MeasuredOn).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OutcomeScoreDto>> ListDtosForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var scores = await ListForPatientAsync(patientId, actor, ct);
        var noteIds = scores.Where(s => s.NoteId != null).Select(s => s.NoteId!.Value).Distinct().ToList();
        var locked = (await _db.ClinicalNotes.AsNoTracking().Where(n => noteIds.Contains(n.Id)).ToListAsync(ct))
            .Where(n => !n.IsEditable).Select(n => n.Id).ToHashSet();
        return scores.Select(s => OutcomeScoreMapper.ToDto(s, s.NoteId is Guid id && locked.Contains(id))).ToList();
    }

    private async Task<bool> IsLockedAsync(Guid? noteId, CancellationToken ct) =>
        noteId is Guid id && await _db.ClinicalNotes.AnyAsync(
            n => n.Id == id && n.Status != Domain.Enums.NoteStatus.Draft && n.Status != Domain.Enums.NoteStatus.ReturnedForCorrection, ct);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

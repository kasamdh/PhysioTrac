using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>The clinical encounter side of a note: its template version,
/// field values, autosave with conflict detection, template-aware signing
/// checks, and the plan of care a signed evaluation creates.</summary>
public partial class ClinicalNoteService
{
    /// <summary>Template field keys that also fill the note's own plan-of-care
    /// and reporting-period columns (read by compliance checks, the plan of
    /// care and reports).</summary>
    private static readonly IReadOnlyDictionary<string, string> MirroredKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["certificationStart"] = nameof(ClinicalNote.PlanOfCareStart),
        ["certificationEnd"] = nameof(ClinicalNote.PlanOfCareEnd),
        ["frequencyPerWeek"] = nameof(ClinicalNote.FrequencyPerWeek),
        ["durationWeeks"] = nameof(ClinicalNote.DurationWeeks),
        ["periodStart"] = nameof(ClinicalNote.PeriodStart),
        ["periodEnd"] = nameof(ClinicalNote.PeriodEnd),
    };

    /// <summary>Note types whose signature creates a new plan of care.</summary>
    private static bool CreatesPlanOfCare(NoteType type) =>
        type is NoteType.Evaluation or NoteType.PelvicHealthEvaluation or NoteType.ReEvaluation or NoteType.Recertification;

    // ------------------------------------------------------------------ read

    public async Task<EncounterDto> GetEncounterAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        var patient = await _db.Patients.AsNoTracking().Include(p => p.ReferringProvider).FirstAsync(p => p.Id == note.PatientId, ct);

        TemplateVersionDto? template = null;
        string? templateName = null;
        if (note.TemplateVersionId is Guid versionId)
        {
            var version = await _db.ClinicalNoteTemplateVersions.AsNoTracking()
                .Include(v => v.Sections).Include(v => v.Fields).Include(v => v.Template)
                .FirstAsync(v => v.Id == versionId, ct);
            templateName = version.Template!.Name;
            template = new TemplateVersionDto(version.Id, version.TemplateId, version.VersionNumber, version.ChangeSummary,
                version.CreatedAt, null, DocumentationTemplateService.ToSectionDtos(version));
        }
        var values = (await _db.ClinicalNoteFieldValues.AsNoTracking().Where(v => v.NoteId == note.Id).ToListAsync(ct))
            .Select(ToValueDto).ToList();

        var appointment = note.AppointmentId is Guid appointmentId
            ? await _db.Appointments.AsNoTracking().Include(a => a.AppointmentType).FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            : null;
        var providers = await _db.Providers.AsNoTracking()
            .Where(p => p.Id == note.TreatingProviderId || p.Id == note.SupervisingProviderId)
            .Select(p => new { p.Id, Name = (p.FirstName + " " + p.LastName).Trim(), p.Credentials })
            .ToListAsync(ct);
        string? ProviderName(Guid? id) => providers.FirstOrDefault(p => p.Id == id) is { } p
            ? p.Credentials is null ? p.Name : $"{p.Name}, {p.Credentials}" : null;

        var diagnoses = await _db.PatientDiagnoses.AsNoTracking().Include(d => d.DiagnosisCode)
            .Where(d => d.PatientId == patient.Id && d.ResolvedDate == null)
            .OrderByDescending(d => d.IsPrimary)
            .Select(d => d.DiagnosisCode!.Code + " " + d.DiagnosisCode.Description).ToListAsync(ct);
        var allergies = await _db.PatientAllergies.AsNoTracking().Where(a => a.PatientId == patient.Id && a.IsActive)
            .Select(a => a.Reaction == null ? a.Allergen : a.Allergen + " (" + a.Reaction + ")").ToListAsync(ct);
        var plan = await _db.PlansOfCare.AsNoTracking()
            .Where(p => p.PatientId == patient.Id && p.Status == PlanOfCareStatus.Active)
            .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(ct);

        var latest = await _db.ClinicalNoteVersions.AsNoTracking().Where(v => v.NoteId == note.Id)
            .OrderByDescending(v => v.VersionNumber).Select(v => new { v.VersionNumber, v.CreatedAt, v.SavedById }).FirstOrDefaultAsync(ct);
        var names = await NamesAsync(new[] { note.TherapistId }.Concat(latest is null ? [] : [latest.SavedById]), ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - patient.DateOfBirth.Year - (today < patient.DateOfBirth.AddYears(today.Year - patient.DateOfBirth.Year) ? 1 : 0);
        var header = new EncounterHeaderDto(
            patient.Id, $"{patient.FirstName} {patient.LastName}".Trim(), patient.DateOfBirth, age, patient.MedicalRecordNumber,
            appointment?.Id, appointment?.StartsAt, appointment?.EndsAt,
            await VisitNumberAsync(note, ct),
            appointment?.AppointmentType?.Name ?? note.NoteType.ToString(),
            names.GetValueOrDefault(note.TherapistId, ""),
            ProviderName(note.TreatingProviderId), ProviderName(note.SupervisingProviderId),
            patient.ReferringProvider is { } rp ? $"{rp.FirstName} {rp.LastName}".Trim() : null,
            diagnoses, allergies, patient.Precautions,
            plan?.Id, plan?.StartDate, plan?.EndDate);

        var pain = await _db.PainAssessments.AsNoTracking().FirstOrDefaultAsync(p => p.NoteId == note.Id, ct);
        var entries = (await _db.NoteInterventions.AsNoTracking().Where(i => i.NoteId == note.Id).OrderBy(i => i.Order).ToListAsync(ct))
            .Select(ToFlowsheetDto).ToList();
        var findings = await _db.BodyChartFindings.AsNoTracking().Where(f => f.NoteId == note.Id).OrderBy(f => f.Order).ToListAsync(ct);

        return new EncounterDto(ClinicalNoteMapper.ToDto(note), templateName, template, values, header,
            latest?.VersionNumber ?? 0, latest?.CreatedAt ?? note.UpdatedAt, latest is null ? null : names.GetValueOrDefault(latest.SavedById),
            pain is null ? null : ToPainDto(pain), findings.Select(ToFindingDto).ToList(), await PreviousChartingAsync(note, ct),
            (await _db.ObjectiveMeasurements.AsNoTracking().Where(m => m.NoteId == note.Id).OrderBy(m => m.Order).ToListAsync(ct))
                .Select(ToMeasurementDto).ToList(),
            (await _db.SpecialTestResults.AsNoTracking().Where(r => r.NoteId == note.Id).OrderBy(r => r.Order).ToListAsync(ct))
                .Select(ToSpecialTestDto).ToList(),
            await MeasurementHistoryAsync(note, ct),
            await SpecialTestHistoryAsync(note, ct),
            entries,
            FlowsheetRules.Analyze(entries, (await _tenantAccess.OrganizationRequiredAsync(actor, ct)).EightMinuteRuleVariant),
            await PreviousFlowsheetAsync(note, ct),
            (await _db.NoteGoalProgress.AsNoTracking().Where(p => p.NoteId == note.Id).OrderBy(p => p.Order).ToListAsync(ct))
                .Select(ToGoalProgressDto).ToList(),
            await NoteOutcomesAsync(note, ct));
    }

    /// <summary>The outcome scores recorded on this note.</summary>
    private async Task<IReadOnlyList<OutcomeScoreDto>> NoteOutcomesAsync(ClinicalNote note, CancellationToken ct) =>
        (await _db.OutcomeScores.AsNoTracking().Where(o => o.NoteId == note.Id).OrderBy(o => o.Measure).ToListAsync(ct))
            .Select(o => OutcomeScoreMapper.ToDto(o, !note.IsEditable)).ToList();

    /// <summary>The visit's number in the patient's current episode: within
    /// its plan of care (counting the evaluation that started it), or among
    /// all of the patient's notes when there is none. Amendments, voided
    /// notes and non-visit notes don't count.</summary>
    private async Task<int> VisitNumberAsync(ClinicalNote note, CancellationToken ct)
    {
        var query = _db.ClinicalNotes.AsNoTracking().Where(n => n.PatientId == note.PatientId && n.AmendsNoteId == null &&
            n.Status != NoteStatus.Voided &&
            n.NoteType != NoteType.Communication && n.NoteType != NoteType.MissedVisit && n.NoteType != NoteType.Addendum &&
            n.NoteType != NoteType.Consultation);
        if (note.PlanOfCareId is Guid planId)
        {
            var sourceNoteId = await _db.PlansOfCare.Where(p => p.Id == planId).Select(p => p.SourceNoteId).FirstAsync(ct);
            query = query.Where(n => n.PlanOfCareId == planId || n.Id == sourceNoteId);
        }
        var earlier = await query.CountAsync(n => n.ServiceDate < note.ServiceDate ||
            (n.ServiceDate == note.ServiceDate && n.CreatedAt < note.CreatedAt), ct);
        return earlier + 1;
    }

    // ------------------------------------------------------------------ save

    public async Task<EncounterSaveResultDto> SaveEncounterAsync(Guid noteId, SaveEncounterRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note) || !note.IsEditable)
        {
            throw new ForbiddenException(note.IsEditable ? "You are not permitted to edit this note." : "This note can no longer be edited.");
        }

        var latest = await _db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id).OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.VersionNumber, v.CreatedAt, v.SavedById }).FirstOrDefaultAsync(ct);
        if ((latest?.VersionNumber ?? 0) != request.BaseSaveVersion)
        {
            var who = latest is null ? null : (await NamesAsync([latest.SavedById], ct)).GetValueOrDefault(latest.SavedById);
            throw new EncounterConflictException(latest?.VersionNumber ?? 0, latest?.CreatedAt ?? note.UpdatedAt, who);
        }

        if (request.Subjective is not null) note.Subjective = request.Subjective;
        if (request.Objective is not null) note.Objective = request.Objective;
        if (request.Interventions is not null) note.Interventions = request.Interventions;
        if (request.Assessment is not null) note.Assessment = request.Assessment;
        if (request.Plan is not null) note.Plan = request.Plan;
        if (ChartJson.Normalize(request.SubjectiveDetailsJson, nameof(request.SubjectiveDetailsJson)) is string subjectiveJson)
            note.SubjectiveDetailsJson = subjectiveJson;
        if (ChartJson.Normalize(request.ObjectiveMeasurementsJson, nameof(request.ObjectiveMeasurementsJson)) is string objectiveJson)
            note.ObjectiveMeasurementsJson = objectiveJson;

        if (request.Pain is not null) await ApplyPainAsync(note, request.Pain, ct);
        if (request.BodyChart is not null) await ApplyBodyChartAsync(note, request.BodyChart, ct);
        if (request.Measurements is not null) await ApplyMeasurementsAsync(note, request.Measurements, ct);
        if (request.SpecialTests is not null) await ApplySpecialTestsAsync(note, request.SpecialTests, ct);
        if (request.Flowsheet is not null) await ApplyFlowsheetAsync(note, request.Flowsheet, actor, ct);
        if (request.GoalProgress is not null) await ApplyGoalProgressAsync(note, request.GoalProgress, ct);

        if (request.Values is { Count: > 0 } values)
        {
            if (note.TemplateVersionId is not Guid versionId)
                throw new TemplateValidationException(["This note has no template, so it has no template fields."]);
            await ApplyFieldValuesAsync(note, versionId, values, ct);
        }
        note.UpdatedAt = DateTimeOffset.UtcNow;

        int saved;
        try
        {
            saved = await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);
        }
        catch (DbUpdateException)
        {
            // Two saves raced past the check: the version number is unique
            // per note, so the second one lands here instead of overwriting.
            throw new EncounterConflictException(request.BaseSaveVersion + 1, DateTimeOffset.UtcNow, null);
        }
        await AuditNoteOnceAsync(note, "note.updated", actor, UpdateAuditWindow, new { saveVersion = saved }, ct);
        var name = (await NamesAsync([actor.UserId], ct)).GetValueOrDefault(actor.UserId);
        return new EncounterSaveResultDto(saved, note.UpdatedAt, name);
    }

    private async Task ApplyFieldValuesAsync(ClinicalNote note, Guid versionId, IReadOnlyList<TemplateFieldValueDto> values, CancellationToken ct)
    {
        var fields = await _db.ClinicalNoteTemplateFields.AsNoTracking().Where(f => f.VersionId == versionId).ToListAsync(ct);
        var dtos = fields.Select(DocumentationTemplateService.ToFieldDto).ToList();
        var errors = TemplateRules.ValidateValues(dtos, values);
        if (errors.Count > 0) throw new TemplateValidationException(errors);

        var byKey = fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        var existing = await _db.ClinicalNoteFieldValues.Where(v => v.NoteId == note.Id).ToListAsync(ct);
        foreach (var v in values)
        {
            var field = byKey[v.Key];
            var typed = Typed(field.FieldType, v);
            var row = existing.FirstOrDefault(e => string.Equals(e.FieldKey, field.Key, StringComparison.OrdinalIgnoreCase));
            if (typed is null)
            {
                if (row is not null) _db.ClinicalNoteFieldValues.Remove(row);
            }
            else if (row is null)
            {
                row = new ClinicalNoteFieldValue { NoteId = note.Id, FieldId = field.Id, FieldKey = field.Key };
                CopyTyped(typed, row);
                _db.ClinicalNoteFieldValues.Add(row);
            }
            else if (!SameValue(row, typed))
            {
                CopyTyped(typed, row);
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
            Mirror(note, field.Key, typed);
        }
    }

    /// <summary>The single stored member a field's type uses; null when empty.</summary>
    private static ClinicalNoteFieldValue? Typed(TemplateFieldType type, TemplateFieldValueDto v)
    {
        var t = new ClinicalNoteFieldValue();
        switch (type)
        {
            case TemplateFieldType.ShortText or TemplateFieldType.LongText or TemplateFieldType.Select or TemplateFieldType.Radio:
                if (string.IsNullOrWhiteSpace(v.Text)) return null;
                t.ValueText = v.Text;
                break;
            case TemplateFieldType.Number or TemplateFieldType.ClinicalMeasurement or TemplateFieldType.PainScale:
                if (v.Number is null) return null;
                t.ValueNumber = v.Number;
                break;
            case TemplateFieldType.Date:
                if (v.Date is null) return null;
                t.ValueDate = v.Date;
                break;
            case TemplateFieldType.Time:
                if (v.Time is null) return null;
                t.ValueTime = v.Time;
                break;
            case TemplateFieldType.Checkbox:
                if (v.Bool is null) return null;
                t.ValueBool = v.Bool;
                break;
            case TemplateFieldType.Multiselect or TemplateFieldType.StructuredTable:
                if (string.IsNullOrWhiteSpace(v.Json) || v.Json.Trim() is "[]" or "null") return null;
                t.ValueJson = v.Json;
                break;
            default:
                return null;
        }
        return t;
    }

    private static void CopyTyped(ClinicalNoteFieldValue from, ClinicalNoteFieldValue to)
    {
        to.ValueText = from.ValueText;
        to.ValueNumber = from.ValueNumber;
        to.ValueDate = from.ValueDate;
        to.ValueTime = from.ValueTime;
        to.ValueBool = from.ValueBool;
        to.ValueJson = from.ValueJson;
    }

    private static bool SameValue(ClinicalNoteFieldValue a, ClinicalNoteFieldValue b) =>
        a.ValueText == b.ValueText && a.ValueNumber == b.ValueNumber && a.ValueDate == b.ValueDate &&
        a.ValueTime == b.ValueTime && a.ValueBool == b.ValueBool && a.ValueJson == b.ValueJson;

    private static void Mirror(ClinicalNote note, string key, ClinicalNoteFieldValue? typed)
    {
        if (!MirroredKeys.TryGetValue(key, out var column)) return;
        switch (column)
        {
            case nameof(ClinicalNote.PlanOfCareStart): note.PlanOfCareStart = typed?.ValueDate; break;
            case nameof(ClinicalNote.PlanOfCareEnd): note.PlanOfCareEnd = typed?.ValueDate; break;
            case nameof(ClinicalNote.PeriodStart): note.PeriodStart = typed?.ValueDate; break;
            case nameof(ClinicalNote.PeriodEnd): note.PeriodEnd = typed?.ValueDate; break;
            case nameof(ClinicalNote.FrequencyPerWeek): note.FrequencyPerWeek = typed?.ValueNumber is decimal f ? (int)f : null; break;
            case nameof(ClinicalNote.DurationWeeks): note.DurationWeeks = typed?.ValueNumber is decimal d ? (int)d : null; break;
        }
    }

    private static TemplateFieldValueDto ToValueDto(ClinicalNoteFieldValue v) =>
        new(v.FieldKey, v.ValueText, v.ValueNumber, v.ValueDate, v.ValueTime, v.ValueBool, v.ValueJson);

    // ------------------------------------------------------------------ pain and body chart

    private async Task ApplyPainAsync(ClinicalNote note, PainAssessmentDto pain, CancellationToken ct)
    {
        var errors = PainRules.Validate(pain);
        if (errors.Count > 0) throw new TemplateValidationException(errors);
        var row = await _db.PainAssessments.FirstOrDefaultAsync(p => p.NoteId == note.Id, ct);
        if (PainRules.IsEmpty(pain))
        {
            if (row is not null) _db.PainAssessments.Remove(row);
            return;
        }
        if (row is null)
        {
            row = new PainAssessment { NoteId = note.Id, PatientId = note.PatientId };
            _db.PainAssessments.Add(row);
        }
        row.Scale = pain.Scale;
        row.Current = pain.Current;
        row.Best = pain.Best;
        row.Worst = pain.Worst;
        row.BeforeTreatment = pain.BeforeTreatment;
        row.AfterTreatment = pain.AfterTreatment;
        row.Location = Clean(pain.Location);
        row.Qualities = pain.Qualities is { Count: > 0 } q ? string.Join('|', q.Distinct()) : null;
        row.Frequency = pain.Frequency;
        row.Duration = Clean(pain.Duration);
        row.Irritability = pain.Irritability;
        row.AggravatingFactors = Clean(pain.AggravatingFactors);
        row.EasingFactors = Clean(pain.EasingFactors);
        row.DailyPattern = Clean(pain.DailyPattern);
        row.SleepImpact = Clean(pain.SleepImpact);
        row.FunctionalImpact = Clean(pain.FunctionalImpact);
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Replaces the note's body chart with the given findings.</summary>
    private async Task ApplyBodyChartAsync(ClinicalNote note, IReadOnlyList<BodyChartFindingDto> findings, CancellationToken ct)
    {
        var errors = PainRules.Validate(findings);
        if (errors.Count > 0) throw new TemplateValidationException(errors);
        _db.BodyChartFindings.RemoveRange(await _db.BodyChartFindings.Where(f => f.NoteId == note.Id).ToListAsync(ct));
        foreach (var (f, i) in findings.Select((f, i) => (f, i)))
        {
            _db.BodyChartFindings.Add(new BodyChartFinding
            {
                NoteId = note.Id,
                PatientId = note.PatientId,
                View = f.View,
                Region = f.Region,
                Side = f.Side,
                X = Math.Round(f.X, 4),
                Y = Math.Round(f.Y, 4),
                FindingType = f.FindingType,
                Severity = f.Severity,
                RadiatesTo = Clean(f.RadiatesTo),
                Annotation = Clean(f.Annotation),
                Comment = Clean(f.Comment),
                Order = i,
            });
        }
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static PainAssessmentDto ToPainDto(PainAssessment p) => new(
        p.Scale, p.Current, p.Best, p.Worst, p.BeforeTreatment, p.AfterTreatment, p.Location,
        p.Qualities?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? [], p.Frequency, p.Duration, p.Irritability,
        p.AggravatingFactors, p.EasingFactors, p.DailyPattern, p.SleepImpact, p.FunctionalImpact);

    private static BodyChartFindingDto ToFindingDto(BodyChartFinding f) =>
        new(f.View, f.Region, f.Side, f.X, f.Y, f.FindingType, f.Severity, f.RadiatesTo, f.Annotation, f.Comment, f.Id);

    /// <summary>Pain and body chart of the patient's most recent signed note
    /// dated before this one (or the same day but written earlier).</summary>
    private async Task<PreviousChartingDto?> PreviousChartingAsync(ClinicalNote note, CancellationToken ct)
    {
        var previous = await _db.ClinicalNotes.AsNoTracking()
            .Where(n => n.PatientId == note.PatientId && n.Id != note.Id &&
                (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked) &&
                (n.ServiceDate < note.ServiceDate || (n.ServiceDate == note.ServiceDate && n.CreatedAt < note.CreatedAt)) &&
                (_db.PainAssessments.Any(p => p.NoteId == n.Id) || _db.BodyChartFindings.Any(f => f.NoteId == n.Id)))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.ServiceDate }).FirstOrDefaultAsync(ct);
        if (previous is null) return null;
        var pain = await _db.PainAssessments.AsNoTracking().FirstOrDefaultAsync(p => p.NoteId == previous.Id, ct);
        var findings = await _db.BodyChartFindings.AsNoTracking().Where(f => f.NoteId == previous.Id).OrderBy(f => f.Order).ToListAsync(ct);
        return new PreviousChartingDto(previous.Id, previous.ServiceDate, pain is null ? null : ToPainDto(pain), findings.Select(ToFindingDto).ToList());
    }

    public async Task<IReadOnlyList<PatientMeasurementDto>> GetMeasurementHistoryAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var rows = await (from m in _db.ObjectiveMeasurements.AsNoTracking()
                          join n in _db.ClinicalNotes.AsNoTracking() on m.NoteId equals n.Id
                          where m.PatientId == patient.Id && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked)
                          select new { n.Id, n.ServiceDate, n.CreatedAt, m.Order, m.Category, m.Item, m.Movement, m.Side, m.Mode, m.Unit, m.NumericValue, m.TextValue })
            .ToListAsync(ct);
        return rows.OrderBy(r => r.ServiceDate).ThenBy(r => r.CreatedAt).ThenBy(r => r.Order)
            .Select(r => new PatientMeasurementDto(r.Id, r.ServiceDate, r.Category, r.Item, r.Movement, r.Side, r.Mode, r.Unit, r.NumericValue, r.TextValue))
            .ToList();
    }

    public async Task<IReadOnlyList<PainHistoryPointDto>> GetPainHistoryAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var rows = await (from p in _db.PainAssessments.AsNoTracking()
                          join n in _db.ClinicalNotes.AsNoTracking() on p.NoteId equals n.Id
                          where p.PatientId == patient.Id && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked)
                          select new { n.Id, n.ServiceDate, n.CreatedAt, p.Scale, p.Current, p.Worst, p.BeforeTreatment, p.AfterTreatment })
            .ToListAsync(ct);
        return rows.OrderBy(r => r.ServiceDate).ThenBy(r => r.CreatedAt)
            .Select(r => new PainHistoryPointDto(r.Id, r.ServiceDate, r.Scale, r.Current, r.Worst, r.BeforeTreatment, r.AfterTreatment))
            .ToList();
    }

    // ------------------------------------------------------------------ measurements and special tests

    private async Task ApplyMeasurementsAsync(ClinicalNote note, IReadOnlyList<ObjectiveMeasurementDto> rows, CancellationToken ct)
    {
        var errors = MeasurementRules.Validate(rows);
        if (errors.Count > 0) throw new TemplateValidationException(errors);
        _db.ObjectiveMeasurements.RemoveRange(await _db.ObjectiveMeasurements.Where(m => m.NoteId == note.Id).ToListAsync(ct));
        foreach (var (m, i) in rows.Select((m, i) => (m, i)))
        {
            _db.ObjectiveMeasurements.Add(new ObjectiveMeasurement
            {
                NoteId = note.Id,
                PatientId = note.PatientId,
                Category = m.Category,
                Item = m.Item.Trim(),
                Movement = Clean(m.Movement),
                Side = m.Side,
                Mode = Clean(m.Mode),
                NumericValue = m.NumericValue,
                TextValue = Clean(m.TextValue),
                Unit = Clean(m.Unit),
                BodyRegion = Clean(m.BodyRegion),
                EndFeel = Clean(m.EndFeel),
                Painful = m.Painful,
                Compensation = Clean(m.Compensation),
                AssistiveDevice = Clean(m.AssistiveDevice),
                AssistanceLevel = Clean(m.AssistanceLevel),
                Surface = Clean(m.Surface),
                Condition = Clean(m.Condition),
                Comment = Clean(m.Comment),
                Order = i,
            });
        }
    }

    private async Task ApplySpecialTestsAsync(ClinicalNote note, IReadOnlyList<SpecialTestResultDto> results, CancellationToken ct)
    {
        var errors = MeasurementRules.Validate(results).ToList();
        var ids = results.Where(r => r.DefinitionId is not null).Select(r => r.DefinitionId!.Value).Distinct().ToList();
        var known = await _db.SpecialTestDefinitions.Where(d => ids.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct);
        if (known.Count != ids.Count) errors.Add("A special test is not in the library.");
        if (errors.Count > 0) throw new TemplateValidationException(errors);
        _db.SpecialTestResults.RemoveRange(await _db.SpecialTestResults.Where(r => r.NoteId == note.Id).ToListAsync(ct));
        foreach (var (t, i) in results.Select((t, i) => (t, i)))
        {
            _db.SpecialTestResults.Add(new SpecialTestResult
            {
                NoteId = note.Id,
                PatientId = note.PatientId,
                DefinitionId = t.DefinitionId,
                TestName = t.TestName.Trim(),
                Specialty = t.Specialty,
                BodyRegion = Clean(t.BodyRegion),
                Side = t.Side,
                Outcome = t.Outcome,
                NumericValue = t.NumericValue,
                Unit = Clean(t.Unit),
                Interpretation = Clean(t.Interpretation),
                Comment = Clean(t.Comment),
                Order = i,
            });
        }
    }

    private static ObjectiveMeasurementDto ToMeasurementDto(ObjectiveMeasurement m) => new(
        m.Category, m.Item, m.Movement, m.Side, m.Mode, m.NumericValue, m.TextValue, m.Unit, m.BodyRegion, m.EndFeel, m.Painful,
        m.Compensation, m.AssistiveDevice, m.AssistanceLevel, m.Surface, m.Condition, m.Comment, m.Id);

    private static SpecialTestResultDto ToSpecialTestDto(SpecialTestResult r) => new(
        r.TestName, r.Outcome, r.DefinitionId, r.Specialty, r.BodyRegion, r.Side, r.NumericValue, r.Unit, r.Interpretation, r.Comment, r.Id);

    /// <summary>Signed notes of the patient other than this one, with their dates,
    /// split into those before this note and all of them (for baselines).</summary>
    private IQueryable<ClinicalNote> SignedNotesOf(ClinicalNote note) =>
        _db.ClinicalNotes.AsNoTracking().Where(n => n.PatientId == note.PatientId && n.Id != note.Id &&
            (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked));

    /// <summary>For every measurement the patient has on signed notes: its
    /// first value (baseline) and the last one before this note (previous).
    /// Values are kept exactly as recorded; nothing is recalculated.</summary>
    private async Task<IReadOnlyList<MeasurementHistoryDto>> MeasurementHistoryAsync(ClinicalNote note, CancellationToken ct)
    {
        var rows = await (from m in _db.ObjectiveMeasurements.AsNoTracking()
                          join n in SignedNotesOf(note) on m.NoteId equals n.Id
                          select new { m.Category, m.Item, m.Movement, m.Side, m.Mode, m.Unit, m.NumericValue, m.TextValue, n.ServiceDate, n.CreatedAt })
            .ToListAsync(ct);
        bool Before(DateOnly d, DateTimeOffset c) => d < note.ServiceDate || (d == note.ServiceDate && c < note.CreatedAt);
        return rows
            .Where(r => r.NumericValue is not null || !string.IsNullOrWhiteSpace(r.TextValue))
            .GroupBy(r => MeasurementRules.Key(r.Category, r.Item, r.Movement, r.Side, r.Mode, r.Unit))
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.ServiceDate).ThenBy(r => r.CreatedAt).ToList();
                var first = ordered[0];
                var previous = ordered.LastOrDefault(r => Before(r.ServiceDate, r.CreatedAt));
                return new MeasurementHistoryDto(first.Category, first.Item, first.Movement, first.Side, first.Mode, first.Unit,
                    new MeasurementPointDto(first.ServiceDate, first.NumericValue, first.TextValue),
                    previous is null ? null : new MeasurementPointDto(previous.ServiceDate, previous.NumericValue, previous.TextValue));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SpecialTestHistoryDto>> SpecialTestHistoryAsync(ClinicalNote note, CancellationToken ct)
    {
        var rows = await (from r in _db.SpecialTestResults.AsNoTracking()
                          join n in SignedNotesOf(note) on r.NoteId equals n.Id
                          where n.ServiceDate < note.ServiceDate || (n.ServiceDate == note.ServiceDate && n.CreatedAt < note.CreatedAt)
                          select new { r.TestName, r.Side, r.Outcome, r.NumericValue, r.Unit, n.ServiceDate, n.CreatedAt })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => (r.TestName.ToLowerInvariant(), r.Side))
            .Select(g => g.OrderBy(r => r.ServiceDate).ThenBy(r => r.CreatedAt).Last())
            .Select(r => new SpecialTestHistoryDto(r.TestName, r.Side, r.ServiceDate, r.Outcome, r.NumericValue, r.Unit))
            .ToList();
    }

    // ------------------------------------------------------------------ flowsheet

    private async Task ApplyFlowsheetAsync(ClinicalNote note, IReadOnlyList<FlowsheetEntryDto> entries, ICurrentUser actor, CancellationToken ct)
    {
        var errors = FlowsheetRules.Validate(entries).ToList();
        var libraryIds = entries.Where(e => e.LibraryItemId is not null).Select(e => e.LibraryItemId!.Value).Distinct().ToList();
        if (await _db.InterventionLibraryItems.CountAsync(i => libraryIds.Contains(i.Id), ct) != libraryIds.Count)
            errors.Add("An intervention is not in the library.");
        var sources = entries.Where(e => e.CarriedForwardFromNoteId is not null).Select(e => e.CarriedForwardFromNoteId!.Value).Distinct().ToList();
        if (sources.Count > 0 && await _db.ClinicalNotes.CountAsync(n => sources.Contains(n.Id) && n.PatientId == note.PatientId, ct) != sources.Count)
            errors.Add("Carried-forward entries must come from this patient's notes.");
        if (errors.Count > 0) throw new TemplateValidationException(errors);

        var existing = await _db.NoteInterventions.Where(i => i.NoteId == note.Id).ToListAsync(ct);
        // Carry-forward is audited when entries from another note first appear.
        var before = existing.Where(i => i.CarriedForwardFromNoteId is not null).GroupBy(i => i.CarriedForwardFromNoteId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var after = entries.Where(e => e.CarriedForwardFromNoteId is not null).GroupBy(e => e.CarriedForwardFromNoteId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        _db.NoteInterventions.RemoveRange(existing);
        foreach (var (e, i) in entries.Select((e, i) => (e, i)))
        {
            _db.NoteInterventions.Add(new NoteIntervention
            {
                NoteId = note.Id,
                Description = e.Description.Trim(),
                BodyRegion = Clean(e.BodyRegion),
                Category = e.Category,
                Minutes = e.Minutes,
                Units = e.Units,
                IsTimed = e.IsTimed,
                PatientResponse = Clean(e.PatientResponse),
                Order = i,
                LibraryItemId = e.LibraryItemId,
                CptCode = Clean(e.CptCode)?.ToUpperInvariant(),
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                Sets = e.Sets,
                Repetitions = e.Repetitions,
                Resistance = Clean(e.Resistance),
                Duration = Clean(e.Duration),
                Distance = Clean(e.Distance),
                Position = Clean(e.Position),
                Equipment = Clean(e.Equipment),
                AssistanceLevel = Clean(e.AssistanceLevel),
                Cueing = Clean(e.Cueing),
                Modification = Clean(e.Modification),
                PainBefore = e.PainBefore,
                PainAfter = e.PainAfter,
                Status = e.Status,
                Comment = Clean(e.Comment),
                CarriedForwardFromNoteId = e.CarriedForwardFromNoteId,
                CarryForwardReviewed = e.CarriedForwardFromNoteId is not null && e.CarryForwardReviewed,
            });
        }

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        foreach (var (source, count) in after)
        {
            var added = count - before.GetValueOrDefault(source);
            if (added <= 0) continue;
            await _audit.RecordAuditEventAsync(actor.UserId, "note.carry_forward", nameof(ClinicalNote), note.Id, organization.Id,
                patientId: note.PatientId, metadata: new { sourceNoteId = source, entries = added }, ct: ct);
        }
    }

    /// <summary>Replaces the note's goal progress. Each row copies the goal
    /// as it reads now (and its current value as "previous"), so the note
    /// keeps the goal as documented even if it is edited later.</summary>
    private async Task ApplyGoalProgressAsync(ClinicalNote note, IReadOnlyList<NoteGoalProgressDto> rows, CancellationToken ct)
    {
        var errors = new List<string>();
        if (rows.GroupBy(r => r.GoalId).Any(g => g.Count() > 1)) errors.Add("A goal appears more than once.");
        var ids = rows.Select(r => r.GoalId).Distinct().ToList();
        var goals = await _db.FunctionalGoals.AsNoTracking().Where(g => ids.Contains(g.Id) && g.PatientId == note.PatientId)
            .ToDictionaryAsync(g => g.Id, ct);
        foreach (var (r, i) in rows.Select((r, i) => (r, i + 1)))
        {
            if (!goals.TryGetValue(r.GoalId, out var goal)) { errors.Add($"Goal {i} is not one of this patient's goals."); continue; }
            if (goal.Status == GoalStatus.Draft) errors.Add($"Goal {i} ({goal.FunctionalTask}) must be approved before progress is recorded.");
            if (!GoalRules.IsRecordable(r.Status)) errors.Add($"Goal {i}: choose Not started, In progress, Met, Partially met or Discontinued.");
            if (r.Comment is { Length: > 2000 }) errors.Add($"Goal {i}: the comment is longer than 2000 characters.");
        }
        if (errors.Count > 0) throw new TemplateValidationException(errors);

        _db.NoteGoalProgress.RemoveRange(await _db.NoteGoalProgress.Where(p => p.NoteId == note.Id).ToListAsync(ct));
        foreach (var (r, i) in rows.Select((r, i) => (r, i)))
        {
            var g = goals[r.GoalId];
            _db.NoteGoalProgress.Add(new NoteGoalProgress
            {
                NoteId = note.Id,
                GoalId = g.Id,
                Order = i,
                GoalVersion = g.Version,
                Term = g.Term,
                FunctionalTask = g.FunctionalTask,
                FunctionalLimitation = g.FunctionalLimitation,
                BaselineValue = g.BaselineValue,
                TargetValue = g.TargetValue,
                Unit = g.Unit,
                MeasurementMethod = g.MeasurementMethod,
                TargetDate = g.TargetDate,
                PreviousValue = g.CurrentValue,
                CurrentValue = r.CurrentValue,
                Status = r.Status,
                Comment = Clean(r.Comment),
                IncludeInNarrative = r.IncludeInNarrative,
            });
        }
    }

    internal static NoteGoalProgressDto ToGoalProgressDto(NoteGoalProgress p) => new(
        p.GoalId, p.CurrentValue, p.Status, p.Comment, p.IncludeInNarrative, p.GoalVersion, p.Term, p.FunctionalTask,
        p.FunctionalLimitation, p.BaselineValue, p.TargetValue, p.Unit, p.MeasurementMethod, p.TargetDate, p.PreviousValue,
        p.ProgressPercent, p.Id);

    internal static FlowsheetEntryDto ToFlowsheetDto(NoteIntervention i) => new(
        i.Description, i.Category, i.CptCode, i.IsTimed, i.StartTime, i.EndTime, i.Minutes, i.Units, i.Sets, i.Repetitions,
        i.Resistance, i.Duration, i.Distance, i.Position, i.Equipment, i.AssistanceLevel, i.Cueing, i.Modification,
        i.PatientResponse, i.PainBefore, i.PainAfter, i.Status, i.Comment, i.BodyRegion, i.LibraryItemId,
        i.CarriedForwardFromNoteId, i.CarryForwardReviewed, i.Id);

    /// <summary>The flowsheet of the patient's last signed note before this
    /// one -- the source for previous-visit comparison and carry-forward.</summary>
    private async Task<PreviousFlowsheetDto?> PreviousFlowsheetAsync(ClinicalNote note, CancellationToken ct)
    {
        var previous = await SignedNotesOf(note)
            .Where(n => (n.ServiceDate < note.ServiceDate || (n.ServiceDate == note.ServiceDate && n.CreatedAt < note.CreatedAt)) &&
                _db.NoteInterventions.Any(i => i.NoteId == n.Id))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.ServiceDate }).FirstOrDefaultAsync(ct);
        if (previous is null) return null;
        var entries = await _db.NoteInterventions.AsNoTracking().Where(i => i.NoteId == previous.Id).OrderBy(i => i.Order).ToListAsync(ct);
        return new PreviousFlowsheetDto(previous.Id, previous.ServiceDate, entries.Select(ToFlowsheetDto).ToList());
    }

    // ------------------------------------------------------------------ open from the schedule

    public async Task<AppointmentEncounterDto> OpenAppointmentEncounterAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default,
        bool missedVisit = false)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var appointment = await _db.Appointments.AsNoTracking().Include(a => a.AppointmentType).Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment?.Patient is null || appointment.Patient.OrganizationId != organization.Id)
            throw new NotFoundException("Appointment was not found.");

        var existing = await _db.ClinicalNotes.Where(n => n.AppointmentId == appointment.Id).Select(n => (Guid?)n.Id).FirstOrDefaultAsync(ct);
        if (existing is Guid noteId) return new AppointmentEncounterDto(noteId, false);

        var missed = appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow;
        if (missed && !missedVisit)
            throw new InvalidOperationException("No treatment note is written for a cancelled or no-show visit. Document it with a missed-visit note.");
        if (missedVisit && !missed)
            throw new InvalidOperationException("Mark the visit cancelled or no-show before writing a missed-visit note.");

        var serviceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.StartsAt, Tz(organization.Timezone)).DateTime);
        var note = await CreateDraftAsync(new CreateNoteRequest(
            appointment.PatientId,
            missedVisit ? NoteType.MissedVisit : EncounterRules.NoteTypeFor(appointment.Kind, appointment.AppointmentType?.DefaultNoteType),
            serviceDate, appointment.Id, null, null, null, null, null, null, null, null, null, null), actor, ct);
        return new AppointmentEncounterDto(note.Id, true);
    }

    private static TimeZoneInfo Tz(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }

    public async Task<EncounterStatusDto> GetEncounterStatusAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        var latest = await _db.ClinicalNoteVersions.AsNoTracking().Where(v => v.NoteId == note.Id)
            .OrderByDescending(v => v.VersionNumber).Select(v => new { v.VersionNumber, v.CreatedAt, v.SavedById }).FirstOrDefaultAsync(ct);
        var name = latest is null ? null : (await NamesAsync([latest.SavedById], ct)).GetValueOrDefault(latest.SavedById);
        return new EncounterStatusDto(latest?.VersionNumber ?? 0, latest?.CreatedAt ?? note.UpdatedAt, name, latest?.SavedById, note.Status);
    }

    // ------------------------------------------------------------------ template choice

    public async Task<ClinicalNote> ChangeTemplateAsync(Guid noteId, Guid templateId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note) || !note.IsEditable) throw new ForbiddenException("This note can no longer be edited.");
        if (await _db.ClinicalNoteFieldValues.AnyAsync(v => v.NoteId == note.Id, ct))
            throw new InvalidOperationException("The template can only be changed before any of its fields are filled in.");
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var version = await DocumentationTemplateService.CurrentVersionIdAsync(_db, organization.Id, templateId, ct)
            ?? throw new NotFoundException("Template was not found.");
        note.TemplateVersionId = version;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);
        return note;
    }

    // ------------------------------------------------------------------ signing checks

    public async Task<IReadOnlyList<ComplianceFinding>> GetComplianceAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        return await ComplianceAsync(note, ct);
    }

    /// <summary>The note's documentation checks plus, for a template-based
    /// note, every required field that is still empty.</summary>
    private async Task<IReadOnlyList<ComplianceFinding>> ComplianceAsync(ClinicalNote note, CancellationToken ct)
    {
        var findings = NoteComplianceEvaluator.Evaluate(note).ToList();
        var unreviewed = (await CurrentRowsAsync(_db.NoteInterventions, note.Id, ct))
            .Count(i => i.CarriedForwardFromNoteId is not null && !i.CarryForwardReviewed);
        if (unreviewed > 0)
        {
            findings.Insert(0, new ComplianceFinding("carry_forward_unreviewed", "high", "Carried-forward interventions need review",
                $"{unreviewed} carried-forward entr{(unreviewed == 1 ? "y has" : "ies have")} not been marked reviewed.", true));
        }
        if (note.AppointmentId is Guid appointmentId && EpisodeRules.IsVisit(note.NoteType) &&
            await _db.Appointments.AnyAsync(a => a.Id == appointmentId &&
                (a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow), ct))
        {
            findings.Insert(0, new ComplianceFinding("visit_cancelled", "high", "The visit was cancelled or a no-show",
                "A treatment note isn't signed for a visit that didn't happen. Void this draft and write a missed-visit note instead.", true));
        }
        if (note.PrefilledAt is not null && note.PrefillReviewedAt is null)
        {
            findings.Insert(0, new ComplianceFinding("prefill_unreviewed", "high", "Pre-filled content needs review",
                "Fields were filled from the patient's signed charting. Review and edit them, then confirm the review.", true));
        }
        if (note.NoteType == NoteType.Progress && (note.FrequencyPerWeek is not null || note.DurationWeeks is not null))
        {
            var plan = await _db.PlansOfCare.AsNoTracking()
                .Where(p => p.PatientId == note.PatientId && p.Status == PlanOfCareStatus.Active)
                .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(ct);
            if (plan is not null && ((note.FrequencyPerWeek is int f && f != plan.FrequencyPerWeek) ||
                (note.DurationWeeks is int w && w != plan.DurationWeeks)))
            {
                findings.Add(new ComplianceFinding("plan_change_needs_recert", "medium", "Plan of care change",
                    $"The frequency or duration differs from the active plan of care ({plan.FrequencyPerWeek?.ToString() ?? "—"}x/week for " +
                    $"{plan.DurationWeeks?.ToString() ?? "—"} weeks). A progress note doesn't change the plan; a recertification or a signed plan update may be needed.",
                    false));
            }
        }
        if (note.TemplateVersionId is Guid versionId)
        {
            var fields = (await _db.ClinicalNoteTemplateFields.AsNoTracking().Where(f => f.VersionId == versionId).ToListAsync(ct))
                .Select(DocumentationTemplateService.ToFieldDto).ToList();
            var values = await CurrentFieldValuesAsync(note.Id, ct);
            var columns = new Dictionary<string, string?>
            {
                ["subjective"] = note.Subjective,
                ["objective"] = note.Objective,
                ["interventions"] = note.Interventions,
                ["assessment"] = note.Assessment,
                ["plan"] = note.Plan,
            };
            var missing = TemplateRules.MissingRequired(fields, values.Select(ToValueDto).ToList(), columns);
            if (missing.Count > 0)
            {
                findings.Insert(0, new ComplianceFinding("missing_required_fields", "high", "Required fields are empty",
                    string.Join(", ", missing), true));
            }
        }
        return findings;
    }

    /// <summary>A note's rows of one kind, including unsaved changes in this context.</summary>
    private async Task<IReadOnlyList<T>> CurrentRowsAsync<T>(DbSet<T> set, Guid noteId, CancellationToken ct)
        where T : class, PhysioTrac.Domain.Common.INoteOwned
    {
        await set.Where(x => x.NoteId == noteId).LoadAsync(ct);
        return set.Local.Where(x => x.NoteId == noteId && _db.Entry(x).State != EntityState.Deleted).ToList();
    }

    private async Task<PainAssessment?> CurrentPainAsync(Guid noteId, CancellationToken ct)
    {
        await _db.PainAssessments.Where(p => p.NoteId == noteId).LoadAsync(ct);
        return _db.PainAssessments.Local.FirstOrDefault(p => p.NoteId == noteId && _db.Entry(p).State != EntityState.Deleted);
    }

    private async Task<IReadOnlyList<BodyChartFinding>> CurrentFindingsAsync(Guid noteId, CancellationToken ct)
    {
        await _db.BodyChartFindings.Where(f => f.NoteId == noteId).LoadAsync(ct);
        return _db.BodyChartFindings.Local
            .Where(f => f.NoteId == noteId && _db.Entry(f).State != EntityState.Deleted).OrderBy(f => f.Order).ToList();
    }

    /// <summary>The note's field values including unsaved changes in this context.</summary>
    private async Task<IReadOnlyList<ClinicalNoteFieldValue>> CurrentFieldValuesAsync(Guid noteId, CancellationToken ct)
    {
        await _db.ClinicalNoteFieldValues.Where(v => v.NoteId == noteId).LoadAsync(ct);
        return _db.ClinicalNoteFieldValues.Local
            .Where(v => v.NoteId == noteId && _db.Entry(v).State != EntityState.Deleted)
            .ToList();
    }

    // ------------------------------------------------------------------ plan of care

    /// <summary>When a note becomes final: an evaluation, re-evaluation or
    /// recertification creates the patient's plan of care from its fields
    /// (the previous active plan is kept as Superseded) and links the
    /// patient's open goals to it. Saved with the signature itself.</summary>
    private async Task ApplyFinalSignatureEffectsAsync(ClinicalNote note, CancellationToken ct)
    {
        await ApplySignedGoalProgressAsync(note, ct);
        if (note.NoteType == NoteType.Discharge) await CloseEpisodeAsync(note, ct);
        if (!CreatesPlanOfCare(note.NoteType)) return;
        var values = (await CurrentFieldValuesAsync(note.Id, ct))
            .ToDictionary(v => v.FieldKey, StringComparer.OrdinalIgnoreCase);
        string? Text(params string[] keys) => keys.Select(k => values.GetValueOrDefault(k)).FirstOrDefault(v => v is not null) is { } v
            ? v.ValueText ?? JoinList(v.ValueJson) : null;

        var start = values.GetValueOrDefault("certificationStart")?.ValueDate ?? note.PlanOfCareStart ?? note.ServiceDate;
        var weeks = note.DurationWeeks;
        var end = values.GetValueOrDefault("certificationEnd")?.ValueDate ?? note.PlanOfCareEnd ?? start.AddDays(7 * (weeks ?? 4));

        var previous = await _db.PlansOfCare.Where(p => p.PatientId == note.PatientId && p.Status == PlanOfCareStatus.Active)
            .OrderByDescending(p => p.StartDate).ToListAsync(ct);
        foreach (var p in previous) p.Status = PlanOfCareStatus.Superseded;

        var plan = new PlanOfCare
        {
            PatientId = note.PatientId,
            SourceNoteId = note.Id,
            PreviousPlanOfCareId = previous.FirstOrDefault()?.Id,
            Status = PlanOfCareStatus.Active,
            StartDate = start,
            EndDate = end < start ? start : end,
            FrequencyPerWeek = note.FrequencyPerWeek,
            DurationWeeks = weeks,
            TreatmentDiagnosis = Text("treatmentDiagnosis", "ptDiagnosis"),
            Prognosis = Text("updatedPrognosis", "prognosis"),
            RehabPotential = Text("rehabPotential"),
            PlannedInterventions = Text("plannedInterventions"),
            HomeProgram = Text("homeExerciseProgram", "homeProgram"),
            PatientEducation = Text("patientEducation"),
            Referrals = Text("referrals"),
        };
        _db.PlansOfCare.Add(plan);
        note.PlanOfCareId = plan.Id;

        foreach (var goal in await _db.FunctionalGoals.Where(g => g.PatientId == note.PatientId &&
            (g.PlanOfCareId == null || previous.Select(p => (Guid?)p.Id).Contains(g.PlanOfCareId)) &&
            g.Status != GoalStatus.Met && g.Status != GoalStatus.Discontinued).ToListAsync(ct))
        {
            goal.PlanOfCareId = plan.Id;
        }
    }

    /// <summary>A signed discharge summary closes the active plan of care,
    /// recording the reason for discharge and the note. The plan itself is
    /// kept (status Discharged).</summary>
    private async Task CloseEpisodeAsync(ClinicalNote note, CancellationToken ct)
    {
        var reason = (await CurrentFieldValuesAsync(note.Id, ct))
            .FirstOrDefault(v => string.Equals(v.FieldKey, "dischargeReason", StringComparison.OrdinalIgnoreCase))?.ValueText;
        var plans = await _db.PlansOfCare.Where(p => p.PatientId == note.PatientId && p.Status == PlanOfCareStatus.Active).ToListAsync(ct);
        foreach (var plan in plans)
        {
            plan.Status = PlanOfCareStatus.Discharged;
            plan.DischargeReason = EpisodeRules.DischargeReasonFor(reason) ?? (reason is null ? null : DischargeReason.Other);
            plan.DischargeNoteId = note.Id;
        }
        note.PlanOfCareId ??= plans.OrderByDescending(p => p.StartDate).FirstOrDefault()?.Id;
    }

    /// <summary>Goal progress documented on a note reaches the goals only
    /// when the note becomes final: each goal takes the value and status,
    /// and a history entry records them with the note.</summary>
    private async Task ApplySignedGoalProgressAsync(ClinicalNote note, CancellationToken ct)
    {
        var rows = await CurrentRowsAsync(_db.NoteGoalProgress, note.Id, ct);
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.GoalId).ToList();
        var goals = await _db.FunctionalGoals.Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        foreach (var row in rows.OrderBy(r => r.Order))
        {
            if (!goals.TryGetValue(row.GoalId, out var goal) || goal.Status == GoalStatus.Draft) continue;
            FunctionalGoalService.ApplyProgress(goal, row.CurrentValue, row.Status);
            goal.UpdatedAt = DateTimeOffset.UtcNow;
            _db.FunctionalGoalHistory.Add(FunctionalGoalService.History(goal, GoalHistoryKind.Progress, note.TherapistId, note.Id, row.Comment));
        }
    }

    private static string? JoinList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            return items is { Count: > 0 } ? string.Join("; ", items) : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}

using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
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

        return new EncounterDto(ClinicalNoteMapper.ToDto(note), templateName, template, values, header,
            latest?.VersionNumber ?? 0, latest?.CreatedAt ?? note.UpdatedAt, latest is null ? null : names.GetValueOrDefault(latest.SavedById));
    }

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

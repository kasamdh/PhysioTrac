using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>The template-driven encounter and the Initial Evaluation:
/// notes start on the right template version, autosave validates values and
/// refuses stale saves, required fields gate signing, and a signed
/// evaluation creates the patient's plan of care.</summary>
public class InitialEvaluationTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, DocumentationTemplateService Templates,
        Organization Org, Patient Pat, TestCurrentUser Therapist, TestCurrentUser Admin);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Avery", LastName = "Sample", DateOfBirth = new DateOnly(1980, 4, 9), Precautions = "Avoid deep knee flexion" };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var tenant = new TenantAccessService(db, audit);
        return new Ctx(db, new ClinicalNoteService(db, tenant, audit, new AcceptAnySignature()), new DocumentationTemplateService(db, tenant, audit),
            org, pat,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin });
    }

    private static Task<ClinicalNote> NewNoteAsync(Ctx c, NoteType type, Guid? appointmentId = null, Guid? templateId = null) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, type, new DateOnly(2026, 10, 7), appointmentId,
            null, null, null, null, null, null, null, null, null, null, TemplateId: templateId), c.Therapist);

    /// <summary>A plausible value for every required, visible template field.</summary>
    private static (List<TemplateFieldValueDto> Values, SaveEncounterRequest Columns) CompleteValues(TemplateVersionDto template)
    {
        var values = new List<TemplateFieldValueDto>();
        string? subjective = null, objective = null, assessment = null, plan = null, interventions = null;
        foreach (var f in template.Sections.SelectMany(s => s.Fields).Where(f => f.IsRequired && f.Condition is null))
        {
            if (f.NoteColumn is not null)
            {
                var text = $"{f.Label} documented.";
                switch (f.NoteColumn)
                {
                    case "subjective": subjective = text; break;
                    case "objective": objective = text; break;
                    case "assessment": assessment = text; break;
                    case "plan": plan = text; break;
                    default: interventions = text; break;
                }
                continue;
            }
            values.Add(f.FieldType switch
            {
                TemplateFieldType.Number or TemplateFieldType.ClinicalMeasurement or TemplateFieldType.PainScale =>
                    new(f.Key, Number: f.Key == "durationWeeks" ? 8 : f.Key == "frequencyPerWeek" ? 2 : f.Validation?.Min ?? 1),
                TemplateFieldType.Date => new(f.Key, Date: f.Key == "certificationEnd" ? new DateOnly(2026, 12, 2) : new DateOnly(2026, 10, 7)),
                TemplateFieldType.Time => new(f.Key, Time: new TimeOnly(9, 0)),
                TemplateFieldType.Checkbox => new(f.Key, Bool: true),
                TemplateFieldType.Select or TemplateFieldType.Radio => new(f.Key, Text: f.Options![0]),
                TemplateFieldType.Multiselect => new(f.Key, Json: $"[\"{f.Options![0]}\"]"),
                TemplateFieldType.StructuredTable => new(f.Key, Json: "[{}]"),
                _ => new(f.Key, Text: $"{f.Label} documented."),
            });
        }
        return (values, new SaveEncounterRequest(0, Subjective: subjective, Objective: objective, Assessment: assessment, Plan: plan, Interventions: interventions));
    }

    private static async Task<int> FillAsync(Ctx c, ClinicalNote note, int saveVersion)
    {
        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        var (values, columns) = CompleteValues(encounter.Template!);
        var saved = await c.Notes.SaveEncounterAsync(note.Id, columns with { BaseSaveVersion = saveVersion, Values = values }, c.Therapist);
        return saved.SaveVersion;
    }

    [Fact]
    public async Task ANewEvaluation_StartsOnTheGeneralEvaluationTemplate_UnlessAnotherIsChosen()
    {
        var c = await SetupAsync();
        var general = await NewNoteAsync(c, NoteType.Evaluation);
        var encounter = await c.Notes.GetEncounterAsync(general.Id, c.Therapist);
        Assert.Equal("Initial Evaluation", encounter.TemplateName);
        Assert.Contains(encounter.Template!.Sections, s => s.Key == "planOfCare");

        var ortho = c.Db.ClinicalNoteTemplates.Single(t => t.TemplateKey == "initial-evaluation-orthopedic");
        var orthoNote = await NewNoteAsync(c, NoteType.Evaluation, templateId: ortho.Id);
        var orthoEncounter = await c.Notes.GetEncounterAsync(orthoNote.Id, c.Therapist);
        Assert.Contains(orthoEncounter.Template!.Sections, s => s.Key == "orthoExam");
    }

    [Fact]
    public async Task TheAppointmentTypesTemplate_IsUsedForItsVisits()
    {
        var c = await SetupAsync();
        var type = new AppointmentType { OrganizationId = c.Org.Id, Name = "Vestibular eval" };
        var appt = new Appointment
        {
            PatientId = c.Pat.Id,
            TherapistId = c.Therapist.UserId,
            AppointmentTypeId = type.Id,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(60)
        };
        c.Db.AddRange(type, appt);
        await c.Db.SaveChangesAsync();
        var vestibular = c.Db.ClinicalNoteTemplates.Single(t => t.TemplateKey == "initial-evaluation-vestibular");
        var copy = await c.Templates.CopyAsync(vestibular.Id, new CopyTemplateRequest("Our vestibular eval"), c.Admin);
        await c.Templates.UpdateAsync(copy.Template.Id, new SaveDocumentationTemplateRequest(copy.Template.Name, NoteType.Evaluation,
            ClinicalSpecialty.Vestibular, copy.CurrentVersion.Sections, AppointmentTypeIds: [type.Id]), c.Admin);

        var note = await NewNoteAsync(c, NoteType.Evaluation, appt.Id);
        Assert.Equal("Our vestibular eval", (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).TemplateName);
    }

    [Fact]
    public async Task Autosave_StoresTypedValues_AndMirrorsThePlanOfCareFields()
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var start = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;

        var result = await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(start,
        [
            new("chiefComplaint", Text: "Right knee pain"),
            new("frequencyPerWeek", Number: 2),
            new("durationWeeks", Number: 8),
            new("certificationStart", Date: new DateOnly(2026, 10, 7)),
            new("plannedInterventions", Json: "[\"Therapeutic exercise\",\"Manual therapy\"]"),
        ], Subjective: "Pain began after a fictional hiking trip."), c.Therapist);

        Assert.Equal(start + 1, result.SaveVersion);
        var reloaded = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal("Right knee pain", reloaded.Values.Single(v => v.Key == "chiefComplaint").Text);
        Assert.Equal((2, 8, new DateOnly(2026, 10, 7)), (reloaded.Note.FrequencyPerWeek!.Value, reloaded.Note.DurationWeeks!.Value, reloaded.Note.PlanOfCareStart!.Value));
        Assert.Equal("Pain began after a fictional hiking trip.", reloaded.Note.Subjective);

        // An emptied value is removed.
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(result.SaveVersion, [new("chiefComplaint")]), c.Therapist);
        Assert.DoesNotContain((await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).Values, v => v.Key == "chiefComplaint");
    }

    [Fact]
    public async Task AStaleSave_IsRefused_InsteadOfOverwritingAnotherEdit()
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var v = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v, [new("chiefComplaint", Text: "First tab")]), c.Therapist);

        var ex = await Assert.ThrowsAsync<EncounterConflictException>(() =>
            c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v, [new("chiefComplaint", Text: "Second tab")]), c.Admin));
        Assert.Equal(v + 1, ex.CurrentSaveVersion);
        Assert.Equal("First tab", (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).Values.Single(x => x.Key == "chiefComplaint").Text);
    }

    [Theory]
    [InlineData("frequencyPerWeek", 9, null)]
    [InlineData("fallRisk", null, "Very high")]
    [InlineData("notAField", null, "x")]
    [InlineData("historyOfPresentCondition", null, "goes in the Subjective column")]
    public async Task InvalidValues_AreRejectedByTheServer(string key, int? number, string? text)
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var v = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;
        await Assert.ThrowsAsync<TemplateValidationException>(() =>
            c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v, [new(key, Text: text, Number: number)]), c.Therapist));
    }

    [Fact]
    public async Task Signing_IsBlockedUntilEveryRequiredFieldIsFilled_IncludingConditionalOnes()
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var compliance = await c.Notes.GetComplianceAsync(note.Id, c.Therapist);
        var missing = compliance.Single(f => f.Code == "missing_required_fields");
        Assert.Contains("Primary complaint", missing.Detail);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw"));

        var version = await FillAsync(c, note, (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion);
        // A positive red-flag screen makes its details field required.
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(version, [new("redFlags", Text: "Positive — addressed")]), c.Therapist);
        var conditional = (await c.Notes.GetComplianceAsync(note.Id, c.Therapist)).Single(f => f.Code == "missing_required_fields");
        Assert.Equal("Red-flag details", conditional.Detail);
    }

    [Fact]
    public async Task ASignedEvaluation_CreatesThePlanOfCare_AndLinksOpenGoals()
    {
        var c = await SetupAsync();
        var goal = new FunctionalGoal { PatientId = c.Pat.Id, AuthorId = c.Therapist.UserId, FunctionalTask = "Climb stairs", Status = GoalStatus.Active, TargetDate = new DateOnly(2026, 12, 1) };
        c.Db.FunctionalGoals.Add(goal);
        await c.Db.SaveChangesAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var version = await FillAsync(c, note, (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion);
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(version,
            [new("treatmentDiagnosis", Text: "Fictional patellofemoral pain"), new("plannedInterventions", Json: "[\"Therapeutic exercise\",\"Manual therapy\"]")]), c.Therapist);

        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var plan = Assert.Single(c.Db.PlansOfCare.Where(p => p.PatientId == c.Pat.Id));
        Assert.Equal((PlanOfCareStatus.Active, note.Id), (plan.Status, plan.SourceNoteId));
        Assert.Equal((new DateOnly(2026, 10, 7), new DateOnly(2026, 12, 2), 2, 8), (plan.StartDate, plan.EndDate, plan.FrequencyPerWeek!.Value, plan.DurationWeeks!.Value));
        Assert.Equal("Fictional patellofemoral pain", plan.TreatmentDiagnosis);
        Assert.Equal("Therapeutic exercise; Manual therapy", plan.PlannedInterventions);
        Assert.Equal(plan.Id, (await c.Db.ClinicalNotes.FindAsync(note.Id))!.PlanOfCareId);
        Assert.Equal(plan.Id, (await c.Db.FunctionalGoals.FindAsync(goal.Id))!.PlanOfCareId);

        var header = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).Header;
        Assert.Equal((plan.Id, "Avoid deep knee flexion", 1), (header.ActivePlanOfCareId!.Value, header.Precautions!, header.VisitNumber));
    }

    [Fact]
    public async Task ANewSignedEvaluation_SupersedesThePreviousPlan()
    {
        var c = await SetupAsync();
        foreach (var _ in new[] { 1, 2 })
        {
            var note = await NewNoteAsync(c, NoteType.Evaluation);
            await FillAsync(c, note, (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion);
            await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
        }
        var plans = c.Db.PlansOfCare.OrderBy(p => p.CreatedAt).ToList();
        Assert.Equal(new[] { PlanOfCareStatus.Superseded, PlanOfCareStatus.Active }, plans.Select(p => p.Status).ToArray());
        Assert.Equal(plans[0].Id, plans[1].PreviousPlanOfCareId);
    }

    [Fact]
    public async Task ASignedEncounter_CannotBeSavedAgain()
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var v = await FillAsync(c, note, (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
        var current = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(current, [new("chiefComplaint", Text: "changed")]), c.Therapist));
        Assert.True(current > v);
    }

    [Fact]
    public async Task TheTemplate_CanBeSwitchedOnlyBeforeFieldsAreFilled()
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        var tmj = c.Db.ClinicalNoteTemplates.Single(t => t.TemplateKey == "initial-evaluation-tmj");
        await c.Notes.ChangeTemplateAsync(note.Id, tmj.Id, c.Therapist);
        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal("Initial Evaluation — TMJ", encounter.TemplateName);

        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(encounter.SaveVersion, [new("mouthOpening", Number: 38)]), c.Therapist);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.ChangeTemplateAsync(note.Id, tmj.Id, c.Therapist));
    }

    [Fact]
    public async Task AnAmendment_CarriesTheTemplateValues()
    {
        var c = await SetupAsync();
        var note = await NewNoteAsync(c, NoteType.Evaluation);
        await FillAsync(c, note, (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var amendment = await c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest("Wrong frequency"), c.Therapist);
        var original = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        var copy = await c.Notes.GetEncounterAsync(amendment.Id, c.Therapist);
        Assert.Equal(original.Template!.Id, copy.Template!.Id);
        Assert.Equal(original.Values.Count, copy.Values.Count);
    }

    [Fact]
    public async Task AmendingTheEvaluation_KeepsCountingTheSameEpisode()
    {
        var c = await SetupAsync();
        var evaluation = await NewNoteAsync(c, NoteType.Evaluation);
        await FillAsync(c, evaluation, (await c.Notes.GetEncounterAsync(evaluation.Id, c.Therapist)).SaveVersion);
        await c.Notes.SignNoteAsync(evaluation.Id, true, null, c.Therapist, "pw");
        var firstVisit = await NewNoteAsync(c, NoteType.Daily);

        var amendment = await c.Notes.CreateAmendmentAsync(evaluation.Id, new CreateAmendmentRequest("Wrong frequency"), c.Therapist);
        await c.Notes.SignNoteAsync(amendment.Id, true, null, c.Therapist, "pw");
        Assert.Equal(2, c.Db.PlansOfCare.Count(p => p.PatientId == c.Pat.Id)); // the corrected plan replaces the first

        var nextVisit = await NewNoteAsync(c, NoteType.Daily);
        Assert.NotEqual(firstVisit.PlanOfCareId, nextVisit.PlanOfCareId);
        // Evaluation, first visit, this visit -- not "visit 1" again.
        Assert.Equal(3, (await c.Notes.GetEncounterAsync(nextVisit.Id, c.Therapist)).Header.VisitNumber);
    }
}

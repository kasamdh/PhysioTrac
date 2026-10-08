using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Documentation on the schedule: opening a note from a visit,
/// one note per visit, cancelled / no-show visits, rescheduling, the
/// calendar's documentation status, and the documentation dashboard.</summary>
public class ScheduleDocumentationTests
{
    private static readonly TimeZoneInfo Ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, AppointmentService Appointments, ScheduleService Schedule,
        Organization Org, Patient Pat, Patient Other, Provider Provider, TestCurrentUser Pt, TestCurrentUser Pta, TestCurrentUser Admin)
    {
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Ny).DateTime);
    }

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", Timezone = "America/New_York" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1975, 6, 6) };
        var other = new Patient { OrganizationId = org.Id, FirstName = "Robin", LastName = "Example", DateOfBirth = new DateOnly(1980, 2, 2) };
        db.Organizations.Add(org);
        db.Patients.AddRange(pat, other);
        await db.SaveChangesAsync();
        var ptUser = TestTherapists.Add(db, org.Id);
        var ptaUser = TestTherapists.Add(db, org.Id, UserRole.Assistant);
        var provider = new Provider
        {
            OrganizationId = org.Id,
            FirstName = "Jordan",
            LastName = "Lee",
            Credentials = "PT, DPT",
            UserId = ptUser,
            Licenses = { TestTherapists.ValidLicense() }
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var tenant = new TenantAccessService(db, audit);
        return new Ctx(db, new ClinicalNoteService(db, tenant, audit, new AcceptAnySignature()),
            new AppointmentService(db, tenant, audit, new RecordingReminderService()), new ScheduleService(db, tenant),
            org, pat, other, provider,
            new TestCurrentUser { UserId = ptUser, OrganizationId = org.Id, Role = UserRole.Therapist },
            new TestCurrentUser { UserId = ptaUser, OrganizationId = org.Id, Role = UserRole.Assistant },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin });
    }

    /// <summary>A visit at 10:00 New York time on a local date.</summary>
    private static async Task<Appointment> VisitAsync(Ctx c, DateOnly date, Patient? patient = null, AppointmentStatus status = AppointmentStatus.Scheduled,
        Guid? typeId = null, Guid? therapist = null)
    {
        var local = date.ToDateTime(new TimeOnly(10, 0));
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Ny), TimeSpan.Zero);
        var a = new Appointment
        {
            PatientId = (patient ?? c.Pat).Id,
            TherapistId = therapist ?? c.Pt.UserId,
            ProviderId = c.Provider.Id,
            StartsAt = start,
            EndsAt = start.AddMinutes(45),
            Status = status,
            AppointmentTypeId = typeId
        };
        c.Db.Appointments.Add(a);
        await c.Db.SaveChangesAsync();
        return a;
    }

    private static async Task FillDailyAsync(Ctx c, Guid noteId, TestCurrentUser who)
    {
        var v = (await c.Notes.GetEncounterAsync(noteId, who)).SaveVersion;
        await c.Notes.SaveEncounterAsync(noteId, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "s", Objective: "o", Assessment: "a", Plan: "p"), who);
    }

    private ClinicalNote Note(Ctx c, Guid id) => c.Db.ClinicalNotes.AsNoTracking().Single(n => n.Id == id);

    [Fact]
    public async Task OpeningAVisit_CreatesTheExpectedNote_LinkedToPatientVisitProviderAndPlan()
    {
        var c = await SetupAsync();
        var type = new AppointmentType { OrganizationId = c.Org.Id, Name = "Progress visit", DefaultNoteType = NoteType.Progress };
        c.Db.AppointmentTypes.Add(type);
        var plan = new PlanOfCare
        {
            PatientId = c.Pat.Id,
            SourceNoteId = Guid.NewGuid(),
            Status = PlanOfCareStatus.Active,
            StartDate = c.Today.AddDays(-20),
            EndDate = c.Today.AddDays(40)
        };
        c.Db.PlansOfCare.Add(plan);
        await c.Db.SaveChangesAsync();
        var visit = await VisitAsync(c, c.Today, typeId: type.Id);

        var opened = await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pt);
        var note = Note(c, opened.NoteId);
        Assert.True(opened.Created);
        Assert.Equal((NoteType.Progress, c.Pat.Id, visit.Id, c.Provider.Id, plan.Id, c.Today),
            (note.NoteType, note.PatientId, note.AppointmentId!.Value, note.TreatingProviderId!.Value, note.PlanOfCareId!.Value, note.ServiceDate));
    }

    [Fact]
    public async Task EachVisitHasOneNote_UnlessTheNoteIsVoided()
    {
        var c = await SetupAsync();
        var visit = await VisitAsync(c, c.Today);
        var first = await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pt);
        Assert.False((await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pta)).Created);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily,
            c.Today, visit.Id, null, null, null, null, null, null, null, null, null, null), c.Admin));
        Assert.Contains("already has a note", ex.Message);
        await Assert.ThrowsAsync<NotFoundException>(() => c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Other.Id, NoteType.Daily,
            c.Today, visit.Id, null, null, null, null, null, null, null, null, null, null), c.Admin));

        // An authorized person voids the note, which frees the visit.
        await c.Notes.VoidNoteAsync(first.NoteId, new VoidNoteRequest("Opened by mistake."), c.Pt);
        var second = await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pt);
        Assert.True(second.Created);
        Assert.NotEqual(first.NoteId, second.NoteId);
    }

    [Fact]
    public async Task CancelledAndNoShowVisits_TakeOnlyAMissedVisitNote()
    {
        var c = await SetupAsync();
        var noShow = await VisitAsync(c, c.Today.AddDays(-1), status: AppointmentStatus.NoShow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.OpenAppointmentEncounterAsync(noShow.Id, c.Pt));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily,
            c.Today, noShow.Id, null, null, null, null, null, null, null, null, null, null), c.Pt));
        var missed = await c.Notes.OpenAppointmentEncounterAsync(noShow.Id, c.Pt, missedVisit: true);
        Assert.Equal(NoteType.MissedVisit, Note(c, missed.NoteId).NoteType);

        var scheduled = await VisitAsync(c, c.Today);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.OpenAppointmentEncounterAsync(scheduled.Id, c.Pt, missedVisit: true));

        // A visit cancelled after its note was started: the treatment note can't be signed.
        var draft = await c.Notes.OpenAppointmentEncounterAsync(scheduled.Id, c.Pt);
        await FillDailyAsync(c, draft.NoteId, c.Pt);
        await c.Appointments.CancelAsync(scheduled.Id, c.Admin);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(draft.NoteId, true, null, c.Pt, "pw"));
        Assert.Contains("visit_cancelled", blocked.Message);
    }

    [Fact]
    public async Task Rescheduling_MovesADraftNote_ButNeverSubmittedDocumentation()
    {
        var c = await SetupAsync();
        var visit = await VisitAsync(c, c.Today.AddDays(2));
        var draft = await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pt);
        var newStart = visit.StartsAt.AddDays(3);
        await c.Appointments.RescheduleAsync(visit.Id, new RescheduleAppointmentRequest(newStart, newStart.AddMinutes(45), null, null, null), c.Admin);
        Assert.Equal(c.Today.AddDays(5), Note(c, draft.NoteId).ServiceDate);
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.service_date_moved");

        var submitted = await VisitAsync(c, c.Today.AddDays(1), therapist: c.Pta.UserId);
        var ptaNote = await c.Notes.OpenAppointmentEncounterAsync(submitted.Id, c.Pta);
        await FillDailyAsync(c, ptaNote.NoteId, c.Pta);
        await c.Notes.SignNoteAsync(ptaNote.NoteId, true, null, c.Pta, "pw");
        var moveTo = submitted.StartsAt.AddDays(1);
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.Appointments.RescheduleAsync(submitted.Id,
            new RescheduleAppointmentRequest(moveTo, moveTo.AddMinutes(45), null, null, null), c.Admin));
        Assert.Contains("already signed or submitted", ex.Message);
        Assert.Equal(c.Today.AddDays(1), Note(c, ptaNote.NoteId).ServiceDate);
    }

    [Fact]
    public async Task TheCalendar_ShowsEachVisitsDocumentationStatus()
    {
        var c = await SetupAsync();
        var undocumented = await VisitAsync(c, c.Today);
        var drafted = await VisitAsync(c, c.Today.AddDays(1));
        var cancelled = await VisitAsync(c, c.Today.AddDays(2), status: AppointmentStatus.Cancelled);
        var draft = await c.Notes.OpenAppointmentEncounterAsync(drafted.Id, c.Pt);

        var range = await c.Schedule.GetRangeAsync(c.Admin, new ScheduleQuery(undocumented.StartsAt.AddDays(-1), undocumented.StartsAt.AddDays(4)));
        var byId = range.Appointments.ToDictionary(a => a.Id);
        Assert.Equal((null, DocumentationStatus.NotStarted), (byId[undocumented.Id].NoteId, byId[undocumented.Id].DocumentationStatus));
        Assert.Equal((draft.NoteId, DocumentationStatus.Draft, NoteType.Daily),
            (byId[drafted.Id].NoteId!.Value, byId[drafted.Id].DocumentationStatus!.Value, byId[drafted.Id].NoteType!.Value));
        Assert.Null(byId[cancelled.Id].DocumentationStatus);
    }

    // ------------------------------------------------------------------ dashboard

    private static async Task<ClinicalNote> SignedAsync(Ctx c, NoteType type, DateOnly date, DateOnly? reassessmentDue = null)
    {
        var n = new ClinicalNote
        {
            PatientId = c.Pat.Id,
            TherapistId = c.Pt.UserId,
            TreatingProviderId = c.Provider.Id,
            NoteType = type,
            ServiceDate = date,
            ReassessmentDue = reassessmentDue
        };
        c.Db.ClinicalNotes.Add(n);
        await c.Db.SaveChangesAsync();
        n.Status = NoteStatus.Signed;
        n.SignedAt = DateTimeOffset.UtcNow;
        await c.Db.SaveChangesAsync();
        return n;
    }

    private static async Task<Guid> DraftAsync(Ctx c, DateOnly date, TestCurrentUser who, bool complete)
    {
        var n = await c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, date, null,
            null, null, null, null, null, null, null, null, null, null), who);
        if (complete) await FillDailyAsync(c, n.Id, who);
        return n.Id;
    }

    [Fact]
    public async Task TheDashboard_ListsEveryDocumentationState_AndDeadline()
    {
        var c = await SetupAsync();
        var today = c.Today;
        var eval = await SignedAsync(c, NoteType.Evaluation, today.AddDays(-30), reassessmentDue: today.AddDays(3));
        c.Db.PlansOfCare.Add(new PlanOfCare
        {
            PatientId = c.Pat.Id,
            SourceNoteId = eval.Id,
            Status = PlanOfCareStatus.Active,
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(10)
        });
        await c.Db.SaveChangesAsync();
        for (var i = 0; i < 9; i++) await SignedAsync(c, NoteType.Daily, today.AddDays(-28 + i * 2));

        var todays = await VisitAsync(c, today);
        var yesterday = await VisitAsync(c, today.AddDays(-1));
        var lastWeek = await VisitAsync(c, today.AddDays(-5));
        var ready = await DraftAsync(c, today, c.Pt, complete: true);
        var stale = await DraftAsync(c, today.AddDays(-4), c.Pt, complete: false);
        var submitted = await DraftAsync(c, today.AddDays(-1), c.Pta, complete: true);
        await c.Notes.SignNoteAsync(submitted, true, null, c.Pta, "pw");
        var returned = await DraftAsync(c, today.AddDays(-2), c.Pta, complete: true);
        await c.Notes.SignNoteAsync(returned, true, null, c.Pta, "pw");
        await c.Notes.ReturnForCorrectionAsync(returned, "Add the gait response.", c.Pt);

        var d = await c.Notes.GetDashboardAsync(new DashboardFilter(), c.Admin);
        Assert.Equal(todays.Id, d.TodaysSchedule.Single().AppointmentId);
        // Visits that have started without a note (today's only once its time has passed).
        var started = new[] { todays, yesterday, lastWeek }.Where(v => v.StartsAt <= DateTimeOffset.UtcNow).Select(v => v.Id).ToList();
        Assert.Equal(started, d.NotStarted.Select(v => v.AppointmentId!.Value));
        Assert.Equal(ready, d.ReadyToSign.Single().NoteId);
        Assert.Equal(stale, d.Drafts.Single().NoteId);
        Assert.Equal(submitted, d.AwaitingCosign.Single().NoteId);
        Assert.Equal((returned, "Add the gait response."), (d.Returned.Single().NoteId!.Value, d.Returned.Single().Detail));
        // Overdue: unsigned more than a day after the visit, documented or not.
        Assert.Equal([lastWeek.Id], d.Overdue.Where(i => i.NoteId is null).Select(i => i.AppointmentId!.Value));
        Assert.Equal([stale, returned], d.Overdue.Where(i => i.NoteId is not null).Select(i => i.NoteId!.Value));
        Assert.Equal("9 visits since the last evaluation or progress note of " + today.AddDays(-30).ToString("MM/dd/yyyy") + " — due at visit 10",
            d.ProgressNotesDue.Single().Detail);
        Assert.False(d.ProgressNotesDue.Single().Overdue);
        Assert.Equal(today.AddDays(3), d.ReevaluationsDue.Single().DueDate);
        Assert.Equal("Certification ends in 10 days", d.ExpiringPlans.Single().Detail);
        Assert.Equal(10, d.RecentlySigned.Count);
        Assert.Equal((1, started.Count, 1, 1, 1, 1, 3), (d.Counts.TodaysVisits, d.Counts.NotStarted, d.Counts.Drafts, d.Counts.ReadyToSign,
            d.Counts.AwaitingCosign, d.Counts.Returned, d.Counts.Overdue));
    }

    [Fact]
    public async Task TheDashboard_Filters_AndIsForClinicalStaff()
    {
        var c = await SetupAsync();
        var today = c.Today;
        var ready = await DraftAsync(c, today, c.Pt, complete: true);
        var stale = await DraftAsync(c, today.AddDays(-4), c.Pt, complete: false);
        await VisitAsync(c, today.AddDays(-1), patient: c.Other);

        var onlyReady = await c.Notes.GetDashboardAsync(new DashboardFilter(Status: DocumentationStatus.ReadyToSign), c.Admin);
        Assert.Equal((1, 0, 0), (onlyReady.ReadyToSign.Count, onlyReady.Drafts.Count, onlyReady.NotStarted.Count));

        var otherPatient = await c.Notes.GetDashboardAsync(new DashboardFilter(PatientId: c.Other.Id), c.Admin);
        Assert.Equal((0, 1), (otherPatient.ReadyToSign.Count + otherPatient.Drafts.Count, otherPatient.NotStarted.Count));

        var recent = await c.Notes.GetDashboardAsync(new DashboardFilter(From: today.AddDays(-1)), c.Admin);
        Assert.Equal([ready], recent.ReadyToSign.Concat(recent.Drafts).Select(i => i.NoteId!.Value));

        var progressOnly = await c.Notes.GetDashboardAsync(new DashboardFilter(NoteType: NoteType.Progress), c.Admin);
        Assert.Empty(progressOnly.ReadyToSign.Concat(progressOnly.Drafts));

        var otherProvider = new Provider { OrganizationId = c.Org.Id, FirstName = "Casey", LastName = "Other", Licenses = { TestTherapists.ValidLicense() } };
        c.Db.Providers.Add(otherProvider);
        await c.Db.SaveChangesAsync();
        var forOther = await c.Notes.GetDashboardAsync(new DashboardFilter(ProviderId: otherProvider.Id), c.Admin);
        Assert.Equal(0, forOther.Counts.ReadyToSign + forOther.Counts.Drafts + forOther.Counts.NotStarted);
        var forMine = await c.Notes.GetDashboardAsync(new DashboardFilter(ProviderId: c.Provider.Id), c.Admin);
        Assert.Equal(new[] { ready, stale }.Order(), forMine.ReadyToSign.Concat(forMine.Drafts).Select(i => i.NoteId!.Value).Order());

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.GetDashboardAsync(new DashboardFilter(From: today, To: today.AddDays(-1)), c.Admin));
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Biller };
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.GetDashboardAsync(new DashboardFilter(), biller));
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Today's workflow list (WorkflowController) and signing a visit
/// note completing its appointment.</summary>
public class WorkflowTests
{
    private static readonly DateOnly Day = new(2026, 3, 10);

    private sealed record Seed(
        PhysioTracDbContext Db, Organization Org, TestCurrentUser Admin, TestCurrentUser Therapist,
        Provider Mine, Provider Other, Patient Pat);

    private static async Task<Seed> SeedAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Org", Slug = "org", Timezone = "UTC" };
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var mine = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen", Discipline = ProviderDiscipline.PT, UserId = therapist.UserId };
        var other = new Provider { OrganizationId = org.Id, FirstName = "Avery", LastName = "Kim", Discipline = ProviderDiscipline.PTA };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Quinn", LastName = "Alvarez", DateOfBirth = new DateOnly(2001, 7, 30) };
        db.Organizations.Add(org);
        db.Providers.AddRange(mine, other);
        db.Patients.Add(pat);
        await db.SaveChangesAsync();
        return new Seed(db, org, admin, therapist, mine, other, pat);
    }

    private static Appointment Appt(Seed s, Provider provider, int hour, AppointmentStatus status = AppointmentStatus.Scheduled,
        AppointmentKind kind = AppointmentKind.FollowUp, DateOnly? day = null)
    {
        var start = new DateTimeOffset((day ?? Day).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);
        var a = new Appointment
        {
            PatientId = s.Pat.Id,
            ProviderId = provider.Id,
            TherapistId = provider.UserId ?? Guid.NewGuid(),
            CreatedById = s.Admin.UserId,
            StartsAt = start,
            EndsAt = start.AddMinutes(30),
            Status = status,
            Kind = kind,
        };
        s.Db.Appointments.Add(a);
        return a;
    }

    private static async Task<WorkflowDayDto> TodayAsync(Seed s, TestCurrentUser actor, Guid? providerId = null, bool all = false)
    {
        var audit = new AuditService(s.Db);
        var controller = new WorkflowController(new TenantAccessService(s.Db, audit), actor, s.Db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var result = Assert.IsType<OkObjectResult>(await controller.Today(providerId, all, Day));
        return Assert.IsType<WorkflowDayDto>(result.Value);
    }

    [Fact]
    public async Task Today_DefaultsToTheCallersOwnProvider_AndOnlyThatDay()
    {
        var s = await SeedAsync();
        var mine9 = Appt(s, s.Mine, 9);
        Appt(s, s.Other, 10);
        Appt(s, s.Mine, 9, day: Day.AddDays(1)); // tomorrow
        Appt(s, s.Mine, 9, day: Day.AddDays(-1)); // yesterday
        await s.Db.SaveChangesAsync();

        var day = await TodayAsync(s, s.Therapist);

        Assert.Equal(s.Mine.Id, day.MyProviderId);
        Assert.Equal(mine9.Id, Assert.Single(day.Appointments).AppointmentId);
        Assert.Equal("Quinn Alvarez", day.Appointments[0].PatientName);
    }

    [Fact]
    public async Task Today_AdminCanPickAProvider_OrSeeEveryone_InTimeOrder()
    {
        var s = await SeedAsync();
        var other10 = Appt(s, s.Other, 10);
        var mine9 = Appt(s, s.Mine, 9);
        await s.Db.SaveChangesAsync();

        Assert.Equal(other10.Id, Assert.Single((await TodayAsync(s, s.Admin, s.Other.Id)).Appointments).AppointmentId);
        var all = await TodayAsync(s, s.Admin, all: true);
        Assert.Equal(new[] { mine9.Id, other10.Id }, all.Appointments.Select(a => a.AppointmentId));
    }

    [Fact]
    public async Task Today_TherapistAlwaysGetsTheirOwnDay_WhenRoleChecksAreOn()
    {
        var s = await SeedAsync();
        Appt(s, s.Other, 10);
        var mine9 = Appt(s, s.Mine, 9);
        await s.Db.SaveChangesAsync();

        var asked = await TodayAsync(s, s.Therapist, s.Other.Id, all: true);

        Assert.True(asked.OwnDayOnly);
        Assert.Equal(mine9.Id, Assert.Single(asked.Appointments).AppointmentId);
    }

    [Fact]
    public async Task Today_AttachesTheLatestNote_AndSuggestsANoteTypeByVisitKind()
    {
        var s = await SeedAsync();
        var eval = Appt(s, s.Mine, 9, kind: AppointmentKind.Evaluation);
        var follow = Appt(s, s.Mine, 10);
        s.Db.ClinicalNotes.AddRange(
            new ClinicalNote { PatientId = s.Pat.Id, TherapistId = s.Therapist.UserId, AppointmentId = eval.Id, NoteType = NoteType.Evaluation, Status = NoteStatus.Draft, ServiceDate = Day, CreatedAt = DateTimeOffset.UtcNow.AddHours(-2) },
            new ClinicalNote { PatientId = s.Pat.Id, TherapistId = s.Therapist.UserId, AppointmentId = eval.Id, NoteType = NoteType.Evaluation, Status = NoteStatus.Signed, ServiceDate = Day, CreatedAt = DateTimeOffset.UtcNow });
        await s.Db.SaveChangesAsync();

        var rows = (await TodayAsync(s, s.Therapist)).Appointments;

        var evalRow = rows.Single(r => r.AppointmentId == eval.Id);
        Assert.Equal(NoteStatus.Signed, evalRow.NoteStatus);
        Assert.Equal(NoteType.Evaluation, evalRow.SuggestedNoteType);
        var followRow = rows.Single(r => r.AppointmentId == follow.Id);
        Assert.Null(followRow.NoteId);
        Assert.Equal(NoteType.Daily, followRow.SuggestedNoteType);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.InProgress)]
    public async Task SigningTheVisitNote_CompletesTheAppointment(AppointmentStatus startingStatus)
    {
        var s = await SeedAsync();
        var appt = Appt(s, s.Mine, 9, startingStatus);
        await s.Db.SaveChangesAsync();
        var audit = new AuditService(s.Db);
        var notes = new ClinicalNoteService(s.Db, new TenantAccessService(s.Db, audit), audit, new AcceptAnySignature());

        var draft = await notes.CreateDraftAsync(new CreateNoteRequest(
            s.Pat.Id, NoteType.Daily, Day, appt.Id, "Feels better", "ROM 0-120", "TherEx 30 min", "Improving", "Continue POC",
            null, null, null, null, null), s.Therapist);
        await notes.SignNoteAsync(draft.Id, attestationConfirmed: true, ipAddress: null, s.Therapist);

        Assert.Equal(AppointmentStatus.Completed, (await s.Db.Appointments.SingleAsync(a => a.Id == appt.Id)).Status);
    }
}

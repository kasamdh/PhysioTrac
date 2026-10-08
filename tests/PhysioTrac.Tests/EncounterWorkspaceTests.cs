using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Daily SOAP / unified encounter: opening the encounter from an
/// appointment (one note per visit, the visit's note type), and the save
/// status the workspace polls to warn about another editor.</summary>
public class EncounterWorkspaceTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Organization Org, Patient Pat, TestCurrentUser Therapist, TestCurrentUser Other);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", Timezone = "America/New_York" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Jesse", LastName = "Example", DateOfBirth = new DateOnly(1992, 2, 2) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        return new Ctx(db, new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new AcceptAnySignature()), org, pat,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin });
    }

    private static async Task<Appointment> AppointmentAsync(Ctx c, AppointmentKind kind = AppointmentKind.FollowUp,
        AppointmentStatus status = AppointmentStatus.CheckedIn, AppointmentType? type = null)
    {
        if (type is not null) c.Db.AppointmentTypes.Add(type);
        // 11:30 PM in New York on Oct 7 is already Oct 8 in UTC -- the service date follows the clinic's day.
        var appt = new Appointment
        {
            PatientId = c.Pat.Id,
            TherapistId = c.Therapist.UserId,
            Kind = kind,
            Status = status,
            AppointmentTypeId = type?.Id,
            StartsAt = new DateTimeOffset(2026, 10, 8, 3, 30, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero),
        };
        c.Db.Appointments.Add(appt);
        await c.Db.SaveChangesAsync();
        return appt;
    }

    [Fact]
    public async Task OpeningAVisit_CreatesItsDailyNoteOnce_OnTheClinicsServiceDate()
    {
        var c = await SetupAsync();
        var appt = await AppointmentAsync(c);

        var first = await c.Notes.OpenAppointmentEncounterAsync(appt.Id, c.Therapist);
        var again = await c.Notes.OpenAppointmentEncounterAsync(appt.Id, c.Other);

        Assert.True(first.Created);
        Assert.Equal((first.NoteId, false), (again.NoteId, again.Created));
        var note = Assert.Single(c.Db.ClinicalNotes.Where(n => n.AppointmentId == appt.Id));
        Assert.Equal((NoteType.Daily, new DateOnly(2026, 10, 7)), (note.NoteType, note.ServiceDate));
        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal("Daily Treatment SOAP Note", encounter.TemplateName);
        Assert.Contains(encounter.Template!.Sections, s => s.Key == "subjective" && s.Component == "painAssessment");
    }

    [Theory]
    [InlineData(AppointmentKind.Evaluation, NoteType.Evaluation)]
    [InlineData(AppointmentKind.Progress, NoteType.Progress)]
    [InlineData(AppointmentKind.Discharge, NoteType.Discharge)]
    public async Task TheVisitKind_DecidesTheNoteType(AppointmentKind kind, NoteType expected)
    {
        var c = await SetupAsync();
        var appt = await AppointmentAsync(c, kind);
        var opened = await c.Notes.OpenAppointmentEncounterAsync(appt.Id, c.Therapist);
        Assert.Equal(expected, (await c.Db.ClinicalNotes.FindAsync(opened.NoteId))!.NoteType);
    }

    [Fact]
    public async Task TheAppointmentTypesNoteType_WinsOverTheKind()
    {
        var c = await SetupAsync();
        var type = new AppointmentType { OrganizationId = c.Org.Id, Name = "Recert visit", DefaultNoteType = NoteType.Recertification };
        var appt = await AppointmentAsync(c, AppointmentKind.FollowUp, type: type);
        var opened = await c.Notes.OpenAppointmentEncounterAsync(appt.Id, c.Therapist);
        Assert.Equal(NoteType.Recertification, (await c.Db.ClinicalNotes.FindAsync(opened.NoteId))!.NoteType);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task NoTreatmentNote_IsStartedForAMissedVisit(AppointmentStatus status)
    {
        var c = await SetupAsync();
        var appt = await AppointmentAsync(c, status: status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.OpenAppointmentEncounterAsync(appt.Id, c.Therapist));
        Assert.Empty(c.Db.ClinicalNotes);
    }

    [Fact]
    public async Task AnotherClinicsAppointment_IsNotFound()
    {
        var c = await SetupAsync();
        var appt = await AppointmentAsync(c);
        var outsider = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Role = UserRole.Admin };
        c.Db.Organizations.Add(new Organization { Id = outsider.OrganizationId!.Value, Name = "Other", Slug = "other" });
        await c.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<PhysioTrac.Application.Common.NotFoundException>(() => c.Notes.OpenAppointmentEncounterAsync(appt.Id, outsider));
    }

    [Fact]
    public async Task TheSaveStatus_ShowsWhoSavedLast()
    {
        var c = await SetupAsync();
        var appt = await AppointmentAsync(c);
        var opened = await c.Notes.OpenAppointmentEncounterAsync(appt.Id, c.Therapist);
        var before = await c.Notes.GetEncounterStatusAsync(opened.NoteId, c.Therapist);

        await c.Notes.SaveEncounterAsync(opened.NoteId, new SaveEncounterRequest(before.SaveVersion, [new("equipment", Text: "Bike")]), c.Other);

        var after = await c.Notes.GetEncounterStatusAsync(opened.NoteId, c.Therapist);
        Assert.Equal((before.SaveVersion + 1, c.Other.UserId, NoteStatus.Draft), (after.SaveVersion, after.SavedById!.Value, after.Status));
    }
}

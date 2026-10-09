using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Tests;

/// <summary>The calendar's read side: Day-view provider columns and
/// summaries, counts, the paged list, patient search, and -- above all --
/// that nothing crosses an organization or caseload boundary.</summary>
public class ScheduleServiceTests
{
    [Fact]
    public async Task Day_ShowsEveryProviderAtTheLocation_WithWorkingHours_Blocks_AndASummary()
    {
        var f = new SchedulingFixture();
        f.Db.ProviderTimeOffs.Add(new ProviderTimeOff { ProviderId = f.Pt.Id, StartDateTime = f.At(12), EndDateTime = f.At(13), Reason = TimeOffReason.Lunch });
        await f.Db.SaveChangesAsync();
        var first = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9), minutes: 60), f.Scheduler);
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(10), minutes: 30, patient: f.OtherPatient), f.Scheduler);
        var cancelled = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(14)), f.Scheduler);
        await f.Appointments.CancelAsync(cancelled.Id, f.Scheduler);

        var day = await f.Schedule.GetDayAsync(f.Scheduler, f.Monday, f.Clinic.Id, null);

        Assert.Equal("America/New_York", day.Timezone);
        Assert.Equal(["John Carter", "Sarah Miller"], day.Providers.Select(p => p.Provider.Name));
        var sarah = day.Providers.Single(p => p.Provider.Id == f.Pt.Id);
        var hours = Assert.Single(sarah.WorkingHours);
        Assert.Equal(("08:00", "17:00"), (hours.Start, hours.End));
        Assert.Equal("Lunch", Assert.Single(sarah.Blocks).Reason);

        Assert.Equal(3, sarah.Summary.Appointments);
        Assert.Equal(1, sarah.Summary.Cancelled);
        Assert.Equal(2, sarah.Summary.Remaining);
        Assert.Equal(540, sarah.Summary.WorkingMinutes);
        Assert.Equal(60, sarah.Summary.BlockedMinutes);
        Assert.Equal(90, sarah.Summary.BookedMinutes);
        Assert.Equal(390, sarah.Summary.AvailableMinutes);
        Assert.Equal(19, sarah.Summary.UtilizationPercent); // 90 / 480 bookable
        Assert.Equal(f.At(9), sarah.Summary.FirstAppointmentAt);

        var card = day.Appointments.Single(a => a.Id == first.Id);
        Assert.Equal(("John Smith", "PT10025", "Sarah Miller", "Follow-Up", "Main Clinic", 60),
            (card.PatientName, card.MedicalRecordNumber, card.ProviderName, card.AppointmentTypeName, card.LocationName, card.DurationMinutes));
    }

    [Theory]
    [InlineData(UserRole.Therapist)]
    [InlineData(UserRole.Assistant)]
    public async Task Day_ForAPtOrPta_ShowsEveryColumnAndAppointment_AndAdminLevelSettings(UserRole role)
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        await f.Appointments.CreateAsync(f.Book(f.Pta, f.At(9), patient: f.OtherPatient), f.Scheduler);
        var clinician = role == UserRole.Therapist ? f.UserFor(f.Pt) : f.UserFor(f.Pta);

        var day = await f.Schedule.GetDayAsync(clinician, f.Monday, null, null);
        var settings = await f.Schedule.GetSettingsAsync(clinician);

        Assert.Equal(2, day.Providers.Count);
        Assert.Equal(2, day.Appointments.Count);
        Assert.Equal(2, settings.Providers.Count);
        Assert.True(settings.CanCreate && settings.CanReschedule && settings.CanOverride && settings.CanManageAvailability);
    }

    [Fact]
    public async Task EveryScheduleQuery_ByAPatientPortalAccount_IsForbidden()
    {
        var f = new SchedulingFixture();
        var portal = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = f.Org.Id, Role = UserRole.Patient };

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Schedule.GetSettingsAsync(portal));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Schedule.GetDayAsync(portal, f.Monday, null, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Schedule.GetRangeAsync(portal, new ScheduleQuery(f.At(0), f.At(23))));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Schedule.GetCountsAsync(portal, f.Monday, f.Monday, null, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Schedule.GetListAsync(portal, new ScheduleQuery(f.At(0), f.At(23)), 1, 25));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Schedule.SearchPatientsAsync(portal, "Smith"));
    }

    [Fact]
    public async Task AnotherOrganizationsSchedule_NeverAppears_AndItsLocationIsNotFound()
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        var otherAdmin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = f.OtherOrg.Id, Role = UserRole.Admin };

        var range = await f.Schedule.GetRangeAsync(otherAdmin, new ScheduleQuery(f.At(0), f.At(23)));
        Assert.Empty(range.Appointments);
        Assert.Empty((await f.Schedule.GetDayAsync(otherAdmin, f.Monday, null, null)).Providers);
        Assert.Empty(await f.Schedule.SearchPatientsAsync(otherAdmin, "Smith"));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Schedule.GetDayAsync(f.Scheduler, f.Monday, f.OtherClinic.Id, null));
    }

    [Fact]
    public async Task Counts_GroupNonCancelledAppointmentsByClinicLocalDay()
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(16, 30)), f.Scheduler); // 20:30/21:30 UTC -- still Monday locally
        var tuesday = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9, date: f.Monday.AddDays(1))), f.Scheduler);
        var cancelled = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(10, date: f.Monday.AddDays(1))), f.Scheduler);
        await f.Appointments.CancelAsync(cancelled.Id, f.Scheduler);

        var counts = await f.Schedule.GetCountsAsync(f.Scheduler, f.Monday, f.Monday.AddDays(6), null, null);

        Assert.Equal([new ScheduleDayCountDto(f.Monday, 2), new ScheduleDayCountDto(f.Monday.AddDays(1), 1)], counts);
    }

    [Fact]
    public async Task List_IsPaginated_AndFiltersByPatient()
    {
        var f = new SchedulingFixture();
        for (var hour = 8; hour < 13; hour++)
        {
            await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(hour), patient: hour % 2 == 0 ? f.Patient : f.OtherPatient), f.Scheduler);
        }

        var page2 = await f.Schedule.GetListAsync(f.Scheduler, new ScheduleQuery(f.At(0), f.At(23, 59)), page: 2, pageSize: 2);
        Assert.Equal(5, page2.Total);
        Assert.Equal([f.At(10), f.At(11)], page2.Items.Select(a => a.StartsAt));

        var smiths = await f.Schedule.GetListAsync(f.Scheduler, new ScheduleQuery(f.At(0), f.At(23, 59), PatientSearch: "Smith"), 1, 25);
        Assert.Equal(3, smiths.Total);
    }

    [Theory]
    [InlineData("Smith")]
    [InlineData("PT10025")]
    [InlineData("0102233")]
    [InlineData("1980-05-17")]
    [InlineData("5/17/1980")]
    public async Task SearchPatients_MatchesNameMrnPhoneOrDateOfBirth(string term)
    {
        var f = new SchedulingFixture();
        var match = Assert.Single(await f.Schedule.SearchPatientsAsync(f.Scheduler, term));
        Assert.Equal(f.Patient.Id, match.Id);
    }

    [Fact]
    public async Task SearchPatients_ForATherapist_SearchesTheWholeRoster_NotJustTheirCaseload()
    {
        var f = new SchedulingFixture();
        Assert.Single(await f.Schedule.SearchPatientsAsync(f.UserFor(f.Pt), "Jones"));
    }
}

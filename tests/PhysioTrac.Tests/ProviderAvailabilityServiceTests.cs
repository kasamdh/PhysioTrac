using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Managing a provider's weekly hours and time off -- and that the
/// changes really drive the booking rules.</summary>
public class ProviderAvailabilityServiceTests
{
    private static ProviderAvailabilityService NewService(SchedulingFixture f)
    {
        var audit = new AuditService(f.Db);
        return new ProviderAvailabilityService(f.Db, new TenantAccessService(f.Db, audit), audit);
    }

    private static WeeklyHoursWindowRequest Window(SchedulingFixture f, Weekday day, string start, string end) =>
        new(day, f.Clinic.Id, start, end);

    [Fact]
    public async Task Get_ReturnsTheWeeklyHours_ToAnyStaffRole()
    {
        var f = new SchedulingFixture();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = f.Org.Id, Role = UserRole.Biller };

        var schedule = await NewService(f).GetAsync(f.Pt.Id, biller);

        Assert.Equal(5, schedule.WeeklyHours.Count);
        Assert.All(schedule.WeeklyHours, w => Assert.Equal(("08:00", "17:00", "Main Clinic"), (w.Start, w.End, w.LocationName)));
        Assert.False(schedule.CanManage);
        Assert.Equal("America/New_York", schedule.Timezone);
    }

    [Fact]
    public async Task ReplaceWeeklyHours_SwapsTheWholePattern_AndTheBookingRulesFollowIt()
    {
        var f = new SchedulingFixture();
        var service = NewService(f);

        var updated = await service.ReplaceWeeklyHoursAsync(f.Pt.Id, new ReplaceWeeklyHoursRequest(
        [
            Window(f, Weekday.Monday, "12:00", "20:00"),
            Window(f, Weekday.Tuesday, "07:00", "11:00"),
        ]), f.Scheduler);

        Assert.Equal(2, updated.WeeklyHours.Count);
        Assert.True(await f.Db.AuditEvents.AnyAsync(e => e.ObjectId == f.Pt.Id && e.Action == "provider.weekly_hours_updated"));

        // Monday 9:00 was bookable under the seeded 08:00-17:00; now it's before hours...
        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.OutsideWorkingHours);
        // ...and Monday 6:30 PM, previously outside, now books.
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(18, 30)), f.Scheduler);
    }

    [Theory]
    [InlineData("17:00", "08:00", "end time must be after")]
    [InlineData("8am", "17:00", "valid time")]
    public async Task ReplaceWeeklyHours_RejectsBadTimes(string start, string end, string message)
    {
        var f = new SchedulingFixture();
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => NewService(f).ReplaceWeeklyHoursAsync(
            f.Pt.Id, new ReplaceWeeklyHoursRequest([Window(f, Weekday.Monday, start, end)]), f.Scheduler));
        Assert.Contains(message, ex.Message);
        Assert.Equal(5, await f.Db.ProviderAvailabilities.CountAsync(a => a.ProviderId == f.Pt.Id)); // unchanged
    }

    [Fact]
    public async Task ReplaceWeeklyHours_RejectsOverlappingWindows_AndLocationsTheProviderIsntAssignedTo()
    {
        var f = new SchedulingFixture();
        var service = NewService(f);

        var overlap = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.ReplaceWeeklyHoursAsync(f.Pt.Id,
            new ReplaceWeeklyHoursRequest([Window(f, Weekday.Monday, "08:00", "12:00"), Window(f, Weekday.Monday, "11:00", "15:00")]), f.Scheduler));
        Assert.Contains("overlaps", overlap.Message);

        var foreign = new WeeklyHoursWindowRequest(Weekday.Monday, f.OtherClinic.Id, "08:00", "12:00");
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.ReplaceWeeklyHoursAsync(f.Pt.Id, new ReplaceWeeklyHoursRequest([foreign]), f.Scheduler));
    }

    [Fact]
    public async Task RepeatingLunch_CreatesOneBlockPerWeekday_AtTheSameLocalTime_AndBlocksBooking()
    {
        var f = new SchedulingFixture();
        var weekdays = new[] { Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday };

        var result = await NewService(f).CreateTimeOffAsync(f.Pt.Id,
            new CreateTimeOffRequest(f.At(12), f.At(13), TimeOffReason.Lunch, null, RepeatUntil: f.Monday.AddDays(13), RepeatDays: weekdays),
            f.Scheduler);

        Assert.Equal(10, result.Created.Count); // two working weeks
        Assert.All(result.Created, t => Assert.Equal(12, TimeZoneInfo.ConvertTime(t.StartsAt, f.Tz).Hour));
        Assert.Empty(result.AffectedAppointments);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            f.Appointments.CreateAsync(f.Book(f.Pt, f.At(12, 15, f.Monday.AddDays(8))), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.TimeOff);
    }

    [Fact]
    public async Task TimeOff_OverExistingAppointments_ReportsThem_WithoutMovingThem()
    {
        var f = new SchedulingFixture();
        var booked = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(14)), f.Scheduler);

        var result = await NewService(f).CreateTimeOffAsync(f.Pt.Id,
            new CreateTimeOffRequest(f.At(13), f.At(17), TimeOffReason.Personal, "Doctor's appointment"), f.Scheduler);

        var affected = Assert.Single(result.AffectedAppointments);
        Assert.Equal((booked.Id, "John Smith"), (affected.Id, affected.PatientName));
        Assert.Equal(AppointmentStatus.Scheduled, (await f.Db.Appointments.FindAsync(booked.Id))!.Status);
    }

    [Fact]
    public async Task CancelTimeOff_FreesTheSlotAgain_AndIsAudited()
    {
        var f = new SchedulingFixture();
        var service = NewService(f);
        var created = await service.CreateTimeOffAsync(f.Pt.Id, new CreateTimeOffRequest(f.At(9), f.At(10), TimeOffReason.Meeting, null), f.Scheduler);

        await service.CancelTimeOffAsync(f.Pt.Id, created.Created[0].Id, f.Scheduler);

        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        Assert.Empty((await service.GetAsync(f.Pt.Id, f.Scheduler)).TimeOff);
        Assert.True(await f.Db.AuditEvents.AnyAsync(e => e.ObjectId == f.Pt.Id && e.Action == "provider.time_off_cancelled"));
    }

    [Theory]
    [InlineData(UserRole.Therapist)]
    [InlineData(UserRole.Biller)]
    public async Task Changes_ByARoleOutsideAvailabilityManagement_AreForbidden(UserRole role)
    {
        var f = new SchedulingFixture();
        var service = NewService(f);
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = f.Org.Id, Role = role };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ReplaceWeeklyHoursAsync(f.Pt.Id, new ReplaceWeeklyHoursRequest([]), user));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateTimeOffAsync(f.Pt.Id, new CreateTimeOffRequest(f.At(9), f.At(10), TimeOffReason.Other, null), user));
    }

    [Fact]
    public async Task AnotherOrganizationsProvider_IsNotFound()
    {
        var f = new SchedulingFixture();
        var otherAdmin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = f.OtherOrg.Id, Role = UserRole.Admin };

        await Assert.ThrowsAsync<NotFoundException>(() => NewService(f).GetAsync(f.Pt.Id, otherAdmin));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewService(f).CreateTimeOffAsync(f.Pt.Id, new CreateTimeOffRequest(f.At(9), f.At(10), TimeOffReason.Other, null), otherAdmin));
    }
}

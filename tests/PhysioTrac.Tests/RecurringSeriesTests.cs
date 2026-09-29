using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Tests;

/// <summary>Multi-day recurring series ("2x weekly for 6 weeks"), the
/// preview that reports which dates conflict, and booking just the clean
/// ones -- plus the single-booking additions (notes, therapist/provider
/// consistency) that came with the New Appointment form.</summary>
public class RecurringSeriesTests
{
    private static CreateAppointmentSeriesRequest Series(
        SchedulingFixture f, DateTimeOffset firstStart, int count = 6, IReadOnlyList<Weekday>? days = null,
        DateOnly? endDate = null, bool skipConflicting = false, int intervalWeeks = 1) =>
        new(f.Patient.Id, f.Pt.UserId!.Value, f.Pt.Id, f.Clinic.Id, null, f.FollowUp.Id, AppointmentKind.FollowUp,
            firstStart, firstStart.AddMinutes(45), intervalWeeks, count, "Knee rehab", "Bring brace",
            days, endDate, skipConflicting);

    private static readonly Weekday[] MonWed = [Weekday.Monday, Weekday.Wednesday];

    [Fact]
    public async Task TwiceWeekly_ForSixVisits_BooksMondaysAndWednesdays_AtTheSameLocalTime()
    {
        var f = new SchedulingFixture();

        var series = await f.Appointments.CreateSeriesAsync(Series(f, f.At(9), count: 6, days: MonWed), f.Scheduler);

        var visits = await f.Db.Appointments.Where(a => a.SeriesId == series.Id).OrderBy(a => a.StartsAt).ToListAsync();
        Assert.Equal(6, visits.Count);
        Assert.Equal(
            [f.Monday, f.Monday.AddDays(2), f.Monday.AddDays(7), f.Monday.AddDays(9), f.Monday.AddDays(14), f.Monday.AddDays(16)],
            visits.Select(v => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(v.StartsAt, f.Tz).DateTime)));
        Assert.All(visits, v =>
        {
            Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(v.StartsAt, f.Tz).DateTime));
            Assert.Equal(TimeSpan.FromMinutes(45), v.EndsAt - v.StartsAt);
            Assert.Equal("Bring brace", v.PrivateNotes);
        });
        Assert.Equal(6, series.OccurrenceCount);
    }

    [Fact]
    public async Task WeeklySeries_AcrossTheFallDstChange_StaysAtNineAmLocal()
    {
        var f = new SchedulingFixture();
        // Monday Oct 26 2026 is EDT (-04:00); Monday Nov 2 is EST (-05:00).
        var oct26 = new DateOnly(2026, 10, 26);
        var request = Series(f, f.At(9, date: oct26), count: 2);

        var preview = await f.Appointments.PreviewSeriesAsync(request, f.Scheduler);

        Assert.Equal([TimeSpan.FromHours(-4), TimeSpan.FromHours(-5)], preview.Occurrences.Select(o => o.StartsAt.Offset));
        Assert.All(preview.Occurrences, o => Assert.Equal(9, TimeZoneInfo.ConvertTime(o.StartsAt, f.Tz).Hour));
    }

    [Fact]
    public async Task EndDate_GeneratesEveryMatchingDayUpToAndIncludingIt()
    {
        var f = new SchedulingFixture();
        var request = Series(f, f.At(9), count: 0, days: MonWed, endDate: f.Monday.AddDays(9)); // through the 2nd Wednesday

        var preview = await f.Appointments.PreviewSeriesAsync(request, f.Scheduler);

        Assert.Equal(4, preview.Occurrences.Count);
    }

    [Fact]
    public async Task Preview_ReportsWhichDatesConflict_AndWhy()
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9, date: f.Monday.AddDays(7)), patient: f.OtherPatient), f.Scheduler);

        var preview = await f.Appointments.PreviewSeriesAsync(Series(f, f.At(9), count: 4, days: MonWed), f.Scheduler);

        Assert.Equal((3, 1), (preview.Bookable, preview.Conflicting));
        var conflict = Assert.Single(preview.Occurrences, o => o.Violations.Count > 0);
        Assert.Equal(f.At(9, date: f.Monday.AddDays(7)), conflict.StartsAt);
        Assert.Equal(SchedulingViolationCodes.DoubleBooked, conflict.Violations[0].Code);
        Assert.Empty(f.Db.AppointmentSeries); // a preview saves nothing
    }

    [Fact]
    public async Task Create_WithAConflict_IsAllOrNothingByDefault_ButSkipConflictingBooksTheRest()
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9, date: f.Monday.AddDays(7)), patient: f.OtherPatient), f.Scheduler);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            f.Appointments.CreateSeriesAsync(Series(f, f.At(9), count: 4, days: MonWed), f.Scheduler));
        Assert.Empty(f.Db.AppointmentSeries);

        var series = await f.Appointments.CreateSeriesAsync(Series(f, f.At(9), count: 4, days: MonWed, skipConflicting: true), f.Scheduler);

        Assert.Equal(3, series.OccurrenceCount);
        Assert.Equal(3, await f.Db.Appointments.CountAsync(a => a.SeriesId == series.Id));
        var audit = await f.Db.AuditEvents.SingleAsync(e => e.ObjectId == series.Id && e.Action == "appointment_series.created");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task EveryOtherWeek_KeepsBothDaysInTheSameWeeks()
    {
        var f = new SchedulingFixture();

        var preview = await f.Appointments.PreviewSeriesAsync(Series(f, f.At(9), count: 4, days: MonWed, intervalWeeks: 2), f.Scheduler);

        Assert.Equal(
            [f.Monday, f.Monday.AddDays(2), f.Monday.AddDays(14), f.Monday.AddDays(16)],
            preview.Occurrences.Select(o => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(o.StartsAt, f.Tz).DateTime)));
    }

    [Fact]
    public async Task Create_SavesNotes_AndRejectsATherapistWhoIsntTheProvider()
    {
        var f = new SchedulingFixture();

        var booked = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)) with { Notes = "  Uses a walker  " }, f.Scheduler);
        Assert.Equal("Uses a walker", booked.PrivateNotes);

        var mismatched = f.Book(f.Pt, f.At(10)) with { TherapistId = f.Pta.UserId!.Value };
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Appointments.CreateAsync(mismatched, f.Scheduler));
        Assert.Contains("provider's own login", ex.Message);
    }

    [Fact]
    public async Task ASeriesLongerThanFiftyTwoVisits_IsRejected()
    {
        var f = new SchedulingFixture();
        var request = Series(f, f.At(9), count: 0, days: MonWed, endDate: f.Monday.AddYears(1));

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Appointments.PreviewSeriesAsync(request, f.Scheduler));
    }
}

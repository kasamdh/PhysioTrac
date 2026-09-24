using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Records every call instead of just logging, so tests can assert
/// a reminder really was (or wasn't) scheduled/cancelled for a given
/// appointment -- NoOpReminderService itself has nothing to assert against.</summary>
public class RecordingReminderService : IReminderService
{
    public readonly List<Guid> Scheduled = new();
    public readonly List<Guid> Cancelled = new();

    public Task ScheduleReminderAsync(Guid appointmentId, DateTimeOffset appointmentStartsAt, CancellationToken ct = default)
    {
        Scheduled.Add(appointmentId);
        return Task.CompletedTask;
    }

    public Task CancelReminderAsync(Guid appointmentId, CancellationToken ct = default)
    {
        Cancelled.Add(appointmentId);
        return Task.CompletedTask;
    }
}

/// <summary>Phase 4: conflict detection across all three named resources
/// (provider/patient/room), status transitions + history, reschedule,
/// recurring series, tenant isolation, role gates, and timezone/DST
/// correctness -- all against AppointmentService directly (EF Core
/// in-memory, matching the rest of this suite; Docker/Testcontainers still
/// isn't available on this dev machine).</summary>
public class AppointmentSchedulingPhase4Tests
{
    private static (PhysioTracDbContext Db, AppointmentService Service, RecordingReminderService Reminders, Organization Org, Patient Patient)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var reminders = new RecordingReminderService();
        var service = new AppointmentService(db, tenantAccess, audit, reminders);
        return (db, service, reminders, org, patient);
    }

    private static TestCurrentUser Scheduler(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Scheduler };

    private static CreateAppointmentRequest Request(
        Guid patientId, Guid therapistId, DateTimeOffset start, DateTimeOffset end,
        Guid? providerId = null, Guid? roomId = null) =>
        new(patientId, therapistId, providerId, null, roomId, null, AppointmentKind.FollowUp, start, end, false, null);

    // ---- Conflict detection: provider, patient, room ----

    [Fact]
    public async Task Create_ConflictingProviderId_IsRejected_EvenWithDifferentTherapists()
    {
        var (db, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30), providerId: provider.Id), actor);

        var overlapping = Request(patient.Id, Guid.NewGuid(), start.AddMinutes(15), start.AddMinutes(45), providerId: provider.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(overlapping, actor));
        Assert.Contains("provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ConflictingPatientId_IsRejected_EvenWithDifferentTherapistsAndProviders()
    {
        var (db, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var providerA = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var providerB = new Provider { OrganizationId = org.Id, FirstName = "Priya", LastName = "Sharma" };
        db.Providers.AddRange(providerA, providerB);
        await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30), providerId: providerA.Id), actor);

        var overlapping = Request(patient.Id, Guid.NewGuid(), start.AddMinutes(15), start.AddMinutes(45), providerId: providerB.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(overlapping, actor));
        Assert.Contains("patient", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ConflictingRoomId_IsRejected_EvenWithDifferentPatientsAndTherapists()
    {
        var (db, service, _, org, patient) = NewService();
        var otherPatient = new Patient { OrganizationId = org.Id, FirstName = "Other", LastName = "Patient", DateOfBirth = new DateOnly(1985, 1, 1) };
        db.Patients.Add(otherPatient);
        await db.SaveChangesAsync();

        var actor = Scheduler(org.Id);
        var roomId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30), roomId: roomId), actor);

        var overlapping = Request(otherPatient.Id, Guid.NewGuid(), start.AddMinutes(15), start.AddMinutes(45), roomId: roomId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(overlapping, actor));
        Assert.Contains("room", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_SameRoomDifferentTimes_Succeeds()
    {
        var (db, service, _, org, patient) = NewService();
        var otherPatient = new Patient { OrganizationId = org.Id, FirstName = "Other", LastName = "Patient", DateOfBirth = new DateOnly(1985, 1, 1) };
        db.Patients.Add(otherPatient);
        await db.SaveChangesAsync();

        var actor = Scheduler(org.Id);
        var roomId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30), roomId: roomId), actor);
        var second = await service.CreateAsync(Request(otherPatient.Id, Guid.NewGuid(), start.AddMinutes(30), start.AddMinutes(60), roomId: roomId), actor);

        Assert.Equal(AppointmentStatus.Scheduled, second.Status);
    }

    // ---- Status transitions + history ----

    [Fact]
    public async Task StatusLifecycle_Scheduled_Confirmed_CheckedIn_Completed_RecordsFullHistory()
    {
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), actor);

        await service.ConfirmAsync(created.Id, actor);
        await service.CheckInAsync(created.Id, actor);
        var completed = await service.CompleteAsync(created.Id, actor);

        Assert.Equal(AppointmentStatus.Completed, completed.Status);
        var history = await service.GetStatusHistoryAsync(created.Id, actor);
        Assert.Equal(
            new[] { (AppointmentStatus?)null, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn },
            history.Select(h => h.FromStatus).ToArray());
        Assert.Equal(
            new[] { AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn, AppointmentStatus.Completed },
            history.Select(h => h.ToStatus).ToArray());
    }

    [Fact]
    public async Task Complete_WithoutCheckingInFirst_Throws()
    {
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(created.Id, actor));
    }

    [Fact]
    public async Task MarkNoShow_AfterCheckIn_Throws_TheyClearlyDidShowUp()
    {
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), actor);
        await service.CheckInAsync(created.Id, actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MarkNoShowAsync(created.Id, actor));
    }

    [Fact]
    public async Task MarkNoShow_FromScheduled_Succeeds_AndCancelsTheReminder()
    {
        var (_, service, reminders, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), actor);

        var result = await service.MarkNoShowAsync(created.Id, actor);

        Assert.Equal(AppointmentStatus.NoShow, result.Status);
        Assert.Contains(created.Id, reminders.Cancelled);
    }

    // ---- Reschedule (drag-and-drop lands here) ----

    [Fact]
    public async Task Reschedule_ToAnOpenSlot_Succeeds_AndSchedulesAFreshReminder()
    {
        var (_, service, reminders, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), actor);
        reminders.Scheduled.Clear();

        var newStart = start.AddDays(1);
        var moved = await service.RescheduleAsync(created.Id, new RescheduleAppointmentRequest(newStart, newStart.AddMinutes(30), null, null, null), actor);

        Assert.Equal(newStart, moved.StartsAt);
        Assert.Contains(created.Id, reminders.Scheduled);
    }

    [Fact]
    public async Task Reschedule_IntoAConflict_Throws_AndLeavesTheOriginalTimeUnchanged()
    {
        var (db, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var therapistId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        await service.CreateAsync(Request(patient.Id, therapistId, start.AddHours(3), start.AddHours(3).AddMinutes(30)), actor);
        var toMove = await service.CreateAsync(Request(patient.Id, therapistId, start, start.AddMinutes(30)), actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RescheduleAsync(
            toMove.Id, new RescheduleAppointmentRequest(start.AddHours(3).AddMinutes(10), start.AddHours(3).AddMinutes(40), null, null, null), actor));

        var unchanged = await db.Appointments.FindAsync(toMove.Id);
        Assert.Equal(start, unchanged!.StartsAt);
    }

    [Fact]
    public async Task Reschedule_ACancelledAppointment_Throws()
    {
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), actor);
        await service.CancelAsync(created.Id, actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RescheduleAsync(
            created.Id, new RescheduleAppointmentRequest(start.AddDays(1), start.AddDays(1).AddMinutes(30), null, null, null), actor));
    }

    // ---- Recurring series ----

    [Fact]
    public async Task CreateSeries_GeneratesTheRequestedOccurrenceCount_SpacedByTheInterval()
    {
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var firstStart = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(14);

        var series = await service.CreateSeriesAsync(new CreateAppointmentSeriesRequest(
            patient.Id, Guid.NewGuid(), null, null, null, null, AppointmentKind.FollowUp,
            firstStart, firstStart.AddMinutes(30), IntervalWeeks: 1, OccurrenceCount: 4, ReasonForVisit: null), actor);

        var loaded = await service.GetSeriesAsync(series.Id, actor);
        Assert.Equal(4, loaded.Occurrences.Count);
        var ordered = loaded.Occurrences.OrderBy(a => a.StartsAt).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            Assert.Equal(firstStart.AddDays(7 * i), ordered[i].StartsAt);
            Assert.Equal(series.Id, ordered[i].SeriesId);
        }
    }

    [Fact]
    public async Task CreateSeries_WithOneConflictingOccurrence_RejectsTheWholeSeries_AndCreatesNoOccurrences()
    {
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var therapistId = Guid.NewGuid();
        var firstStart = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(14);

        // Blocks the 3rd occurrence (firstStart + 14 days) for the same therapist.
        var thirdOccurrenceStart = firstStart.AddDays(14);
        await service.CreateAsync(Request(patient.Id, therapistId, thirdOccurrenceStart, thirdOccurrenceStart.AddMinutes(30)), actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSeriesAsync(new CreateAppointmentSeriesRequest(
            patient.Id, therapistId, null, null, null, null, AppointmentKind.FollowUp,
            firstStart, firstStart.AddMinutes(30), IntervalWeeks: 1, OccurrenceCount: 4, ReasonForVisit: null), actor));

        var seriesCreated = await service.ListForRangeAsync(actor, firstStart.AddDays(-1), firstStart.AddDays(30));
        // Only the one blocking appointment exists -- the series created nothing.
        Assert.Single(seriesCreated);
    }

    [Fact]
    public async Task CancelSeries_CancelsOnlyFutureScheduledOccurrences_LeavesCompletedAndPastAlone()
    {
        var (db, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        // Weekly for 4 occurrences starting 10 days ago lands at -10/-3/+4/+11
        // days -- two clearly past, two clearly future, with margin on both
        // sides of "now" so this isn't sensitive to the few milliseconds
        // between capturing firstStart here and CancelSeriesAsync's own
        // DateTimeOffset.UtcNow call later.
        var firstStart = DateTimeOffset.UtcNow.AddDays(-10);

        var series = await service.CreateSeriesAsync(new CreateAppointmentSeriesRequest(
            patient.Id, Guid.NewGuid(), null, null, null, null, AppointmentKind.FollowUp,
            firstStart, firstStart.AddMinutes(30), IntervalWeeks: 1, OccurrenceCount: 4, ReasonForVisit: null), actor);

        var loaded = await service.GetSeriesAsync(series.Id, actor);
        var ordered = loaded.Occurrences.OrderBy(a => a.StartsAt).ToList();
        // occurrences 0 and 1 are in the past (-10, -3 days); mark #0 Completed to prove it's untouched either way.
        ordered[0].Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        var cancelledCount = await service.CancelSeriesAsync(series.Id, actor);

        // Only occurrences 2 and 3 (still in the future, still Scheduled) get cancelled.
        Assert.Equal(2, cancelledCount);
        var reloaded = await service.GetSeriesAsync(series.Id, actor);
        var byStart = reloaded.Occurrences.OrderBy(a => a.StartsAt).ToList();
        Assert.Equal(AppointmentStatus.Completed, byStart[0].Status);
        Assert.Equal(AppointmentStatus.Scheduled, byStart[1].Status); // past, untouched
        Assert.Equal(AppointmentStatus.Cancelled, byStart[2].Status);
        Assert.Equal(AppointmentStatus.Cancelled, byStart[3].Status);
    }

    // ---- Tenant isolation & role permissions ----

    [Fact]
    public async Task Reschedule_AnotherOrganizationsAppointment_ThrowsNotFound()
    {
        var (db, service, _, org, patient) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        var otherPatient = new Patient { OrganizationId = otherOrg.Id, FirstName = "Other", LastName = "Org", DateOfBirth = new DateOnly(1980, 1, 1) };
        db.Organizations.Add(otherOrg);
        db.Patients.Add(otherPatient);
        await db.SaveChangesAsync();

        var otherOrgActor = Scheduler(otherOrg.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), Scheduler(org.Id));

        await Assert.ThrowsAsync<NotFoundException>(() => service.RescheduleAsync(
            created.Id, new RescheduleAppointmentRequest(start.AddDays(1), start.AddDays(1).AddMinutes(30), null, null, null), otherOrgActor));
    }

    [Fact]
    public async Task GetStatusHistory_AnotherOrganizationsAppointment_ThrowsNotFound()
    {
        var (db, service, _, org, patient) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), Scheduler(org.Id));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStatusHistoryAsync(created.Id, Scheduler(otherOrg.Id)));
    }

    [Fact]
    public async Task Create_ByBillerRole_Throws_NotInRoleSetsScheduling()
    {
        var (_, service, _, org, patient) = NewService();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        var start = DateTimeOffset.UtcNow.AddDays(1);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), biller));
    }

    [Fact]
    public async Task Reschedule_ByBillerRole_Throws()
    {
        var (_, service, _, org, patient) = NewService();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(Request(patient.Id, Guid.NewGuid(), start, start.AddMinutes(30)), Scheduler(org.Id));

        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RescheduleAsync(
            created.Id, new RescheduleAppointmentRequest(start.AddDays(1), start.AddDays(1).AddMinutes(30), null, null, null), biller));
    }

    [Fact]
    public async Task CreateSeries_ByBillerRole_Throws()
    {
        var (_, service, _, org, patient) = NewService();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        var firstStart = DateTimeOffset.UtcNow.AddDays(1);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateSeriesAsync(new CreateAppointmentSeriesRequest(
            patient.Id, Guid.NewGuid(), null, null, null, null, AppointmentKind.FollowUp,
            firstStart, firstStart.AddMinutes(30), IntervalWeeks: 1, OccurrenceCount: 2, ReasonForVisit: null), biller));
    }

    // ---- Timezone / DST correctness ----

    [Fact]
    public async Task Create_ConflictDetection_IsCorrectAcrossDifferentUtcOffsets_NotNaiveWallClockComparison()
    {
        // Same absolute instant, expressed with two different UTC offsets
        // (as if one client is in a -05:00 zone and another -04:00) --
        // DateTimeOffset comparison must catch this as the same moment in
        // time, not silently treat differing offsets as different times.
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var therapistId = Guid.NewGuid();

        var estStart = new DateTimeOffset(2026, 11, 15, 14, 0, 0, TimeSpan.FromHours(-5)); // 2:00 PM EST
        var edtEquivalentStart = estStart.ToOffset(TimeSpan.FromHours(-4)); // same instant, expressed as if EDT

        await service.CreateAsync(Request(patient.Id, therapistId, estStart, estStart.AddMinutes(30)), actor);

        var overlapping = Request(patient.Id, therapistId, edtEquivalentStart.AddMinutes(10), edtEquivalentStart.AddMinutes(40));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(overlapping, actor));
    }

    [Fact]
    public async Task Create_AcrossADaylightSavingTransition_PreservesTheCorrectAbsoluteDuration()
    {
        // US "spring forward" in 2026 is March 8th, 2 AM -> 3 AM local
        // (America/New_York). An appointment booked as "starts 1:30 AM,
        // ends 3:00 AM local" on that date is only 30 minutes of real
        // elapsed time, not 90 -- DateTimeOffset arithmetic must reflect
        // that, since the wall clock skips an hour but time itself doesn't.
        var (_, service, _, org, patient) = NewService();
        var actor = Scheduler(org.Id);

        var beforeSpringForward = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5)); // 1:30 AM EST
        var afterSpringForward = new DateTimeOffset(2026, 3, 8, 3, 0, 0, TimeSpan.FromHours(-4)); // 3:00 AM EDT

        var created = await service.CreateAsync(new CreateAppointmentRequest(
            patient.Id, Guid.NewGuid(), null, null, null, null, AppointmentKind.FollowUp,
            beforeSpringForward, afterSpringForward, false, null), actor);

        Assert.Equal(TimeSpan.FromMinutes(30), created.EndsAt - created.StartsAt);
    }
}

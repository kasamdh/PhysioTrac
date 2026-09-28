using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Tests;

/// <summary>Every rule in SchedulingRulesValidator, as enforced through the
/// real booking paths (create, reschedule/move, and the move dry run), plus
/// who may override which rules.</summary>
public class SchedulingRulesTests
{
    private static async Task<SchedulingConflictException> Rejected(Func<Task> booking) =>
        await Assert.ThrowsAsync<SchedulingConflictException>(booking);

    [Fact]
    public async Task Create_InsideWorkingHours_ForALicensedActiveProvider_Succeeds()
    {
        var f = new SchedulingFixture();
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public async Task Create_ForAnInactiveProvider_IsRejected_AndNotOverridable()
    {
        var f = new SchedulingFixture();
        f.Pt.IsActive = false;
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9), overrideReason: "Covering"), f.Admin));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.ProviderInactive);
        Assert.False(ex.CanOverride);
    }

    [Fact]
    public async Task Create_ForAProviderWithAnExpiredLicense_IsRejected()
    {
        var f = new SchedulingFixture();
        (await f.Db.ProviderLicenses.FirstAsync(l => l.ProviderId == f.Pt.Id)).ExpirationDate = f.Monday.AddDays(-1);
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.LicenseInvalid && !v.Overridable);
    }

    [Theory]
    [InlineData(ProviderLicenseStatus.Suspended)]
    [InlineData(ProviderLicenseStatus.Revoked)]
    public async Task Create_ForAProviderWhoseLicenseIsNotActive_IsRejected(ProviderLicenseStatus status)
    {
        var f = new SchedulingFixture();
        (await f.Db.ProviderLicenses.FirstAsync(l => l.ProviderId == f.Pt.Id)).Status = status;
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.LicenseInvalid);
    }

    [Fact]
    public async Task Create_WhenTheOnlyLicenseIsForAnotherState_IsRejected()
    {
        var f = new SchedulingFixture();
        (await f.Db.ProviderLicenses.FirstAsync(l => l.ProviderId == f.Pt.Id)).State = "TX";
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler));
        var violation = Assert.Single(ex.Violations, v => v.Code == SchedulingViolationCodes.LicenseInvalid);
        Assert.Contains("NC", violation.Message);
    }

    [Fact]
    public async Task Create_AtALocationTheProviderDoesntWorkAt_IsRejected()
    {
        var f = new SchedulingFixture();
        var satellite = new Location { OrganizationId = f.Org.Id, Name = "Satellite", State = "NC", Timezone = "America/New_York" };
        f.Db.Locations.Add(satellite);
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9), locationId: satellite.Id), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.ProviderNotAtLocation);
    }

    [Theory]
    [InlineData(AppointmentKind.Evaluation)]
    [InlineData(AppointmentKind.ReEvaluation)]
    [InlineData(AppointmentKind.Discharge)]
    public async Task Create_APtOnlyVisitForAPta_IsRejected(AppointmentKind kind)
    {
        var f = new SchedulingFixture();
        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pta, f.At(9), kind: kind), f.Admin));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.PtaScope && !v.Overridable);
    }

    [Fact]
    public async Task Create_AFollowUpForAPta_Succeeds()
    {
        var f = new SchedulingFixture();
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pta, f.At(9)), f.Scheduler);
        Assert.Equal(f.Pta.Id, appointment.ProviderId);
    }

    [Fact]
    public async Task Create_AnAppointmentTypeTheProviderIsntSetUpFor_IsRejected()
    {
        var f = new SchedulingFixture();
        var dryNeedling = new AppointmentType { OrganizationId = f.Org.Id, Name = "Dry Needling" };
        f.Db.AppointmentTypes.Add(dryNeedling);
        f.Db.ProviderAppointmentTypes.Add(new ProviderAppointmentType { ProviderId = f.Pt.Id, AppointmentTypeId = dryNeedling.Id });
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9), appointmentTypeId: f.FollowUp.Id), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.AppointmentTypeNotAllowed);

        // ...but the type they *are* set up for still books.
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(10), appointmentTypeId: dryNeedling.Id), f.Scheduler);
    }

    [Fact]
    public async Task Create_OutsideWorkingHours_IsRejected_ForAScheduler_WithNoOverrideOffered()
    {
        var f = new SchedulingFixture();
        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(18)), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.OutsideWorkingHours && v.Overridable);
        Assert.False(ex.CanOverride);
    }

    [Fact]
    public async Task Create_OutsideWorkingHours_ByAnAdminWithAReason_IsBooked_AndTheOverrideIsAudited()
    {
        var f = new SchedulingFixture();

        var withoutReason = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(18)), f.Admin));
        Assert.True(withoutReason.CanOverride);

        var booked = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(18), overrideReason: "Patient can only come after work"), f.Admin);

        var audit = await f.Db.AuditEvents.SingleAsync(e => e.ObjectId == booked.Id && e.Action == "schedule.conflict_override");
        Assert.Equal(f.Admin.UserId, audit.ActorId);
    }

    [Fact]
    public async Task Create_DuringLunchTimeOff_IsRejected()
    {
        var f = new SchedulingFixture();
        f.Db.ProviderTimeOffs.Add(new ProviderTimeOff { ProviderId = f.Pt.Id, StartDateTime = f.At(12), EndDateTime = f.At(13), Reason = TimeOffReason.Lunch });
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(12, 15)), f.Scheduler));
        var violation = Assert.Single(ex.Violations, v => v.Code == SchedulingViolationCodes.TimeOff);
        Assert.Contains("lunch", violation.Message);
    }

    [Fact]
    public async Task Create_WhenTheLocationIsClosed_IsRejected()
    {
        var f = new SchedulingFixture();
        f.Db.LocationClosures.Add(new LocationClosure { LocationId = f.Clinic.Id, StartDateTime = f.At(0), EndDateTime = f.At(23, 59), Reason = "Holiday" });
        await f.Db.SaveChangesAsync();

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.LocationClosed);
    }

    [Fact]
    public async Task DoubleBooking_NamesTheProviderAndTimes_AndCantBeOverridden_UnlessTheOrganizationAllowsIt()
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9), minutes: 60), f.Scheduler);
        var overlapping = f.Book(f.Pt, f.At(9, 30), patient: f.OtherPatient, overrideReason: "Urgent add-on");

        var ex = await Rejected(() => f.Appointments.CreateAsync(overlapping, f.Admin));
        var violation = Assert.Single(ex.Violations, v => v.Code == SchedulingViolationCodes.DoubleBooked);
        Assert.Equal("Sarah Miller already has an appointment from 9:00 AM to 10:00 AM.", violation.Message);
        Assert.False(ex.CanOverride);

        f.Db.BookingConfigurations.Add(new BookingConfiguration { OrganizationId = f.Org.Id, AllowDoubleBookOverride = true });
        await f.Db.SaveChangesAsync();

        var booked = await f.Appointments.CreateAsync(overlapping, f.Admin);
        Assert.True(await f.Db.AuditEvents.AnyAsync(e => e.ObjectId == booked.Id && e.Action == "schedule.conflict_override"));
    }

    [Fact]
    public async Task DoubleBookingTheSamePatient_IsNeverOverridable()
    {
        var f = new SchedulingFixture();
        f.Db.BookingConfigurations.Add(new BookingConfiguration { OrganizationId = f.Org.Id, AllowDoubleBookOverride = true });
        await f.Db.SaveChangesAsync();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);

        var ex = await Rejected(() => f.Appointments.CreateAsync(f.Book(f.Pta, f.At(9), overrideReason: "Try anyway"), f.Admin));
        Assert.Contains(ex.Violations, v => v.Code == SchedulingViolationCodes.PatientConflict);
        Assert.False(ex.CanOverride);
    }

    // ---- Moves: reschedule + dry run ----

    [Fact]
    public async Task ValidateMove_ToAnotherPt_ReportsFromAndTo_WithoutChangingAnything()
    {
        var f = new SchedulingFixture();
        var monika = f.AddProvider("Monika", "Pokhrel", ProviderDiscipline.PT);
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);

        var check = await f.Appointments.ValidateRescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(f.At(10, 30), f.At(11), monika.Id, null, null), f.Scheduler);

        Assert.True(check.IsValid);
        Assert.Equal("John Smith", check.PatientName);
        Assert.Equal("Sarah Miller", check.From.ProviderName);
        Assert.Equal("Monika Pokhrel", check.To.ProviderName);
        Assert.Equal(f.At(10, 30), check.To.StartsAt);

        var unchanged = await f.Db.Appointments.FindAsync(appointment.Id);
        Assert.Equal(f.Pt.Id, unchanged!.ProviderId);
        Assert.Equal(f.At(9), unchanged.StartsAt);
    }

    [Fact]
    public async Task ValidateMove_ToAnExpiredLicenseProvider_ReportsTheViolation_AndTheRealMoveIsRejected()
    {
        var f = new SchedulingFixture();
        var expired = f.AddProvider("Evan", "Expired", ProviderDiscipline.PT);
        (await f.Db.ProviderLicenses.FirstAsync(l => l.ProviderId == expired.Id)).ExpirationDate = f.Monday.AddDays(-30);
        await f.Db.SaveChangesAsync();
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        var move = new RescheduleAppointmentRequest(f.At(9), f.At(9, 30), expired.Id, null, null);

        var check = await f.Appointments.ValidateRescheduleAsync(appointment.Id, move, f.Scheduler);
        Assert.False(check.IsValid);
        Assert.Contains(check.Violations, v => v.Code == SchedulingViolationCodes.LicenseInvalid);

        // The dry run is advisory only -- the real move re-checks and refuses.
        await Rejected(() => f.Appointments.RescheduleAsync(appointment.Id, move, f.Scheduler));
        Assert.Equal(f.Pt.Id, (await f.Db.Appointments.FindAsync(appointment.Id))!.ProviderId);
    }

    [Fact]
    public async Task Move_ToAnotherPt_AtANewTime_IsSaved_AndAuditedAsAProviderChange()
    {
        var f = new SchedulingFixture();
        var monika = f.AddProvider("Monika", "Pokhrel", ProviderDiscipline.PT);
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);

        var moved = await f.Appointments.RescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(f.At(10, 30), f.At(11), monika.Id, null, null), f.Scheduler);

        Assert.Equal(monika.Id, moved.ProviderId);
        Assert.Equal(monika.UserId, moved.TherapistId);
        Assert.Equal(f.At(10, 30), moved.StartsAt);
        Assert.True(await f.Db.AuditEvents.AnyAsync(e => e.ObjectId == appointment.Id && e.Action == "appointment.provider_changed"));
    }

    [Fact]
    public async Task Move_ByATherapist_WhenTheOrganizationDisallowsIt_IsForbidden()
    {
        var f = new SchedulingFixture();
        f.Db.BookingConfigurations.Add(new BookingConfiguration { OrganizationId = f.Org.Id, TherapistsMayReschedule = false });
        await f.Db.SaveChangesAsync();
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Appointments.RescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(f.At(10), f.At(10, 30), null, null, null), f.UserFor(f.Pt)));
    }

    // ---- Start visit ----

    [Fact]
    public async Task StartVisit_MovesCheckedInToInProgress_ThenComplete_AndAnInProgressVisitCantBeRescheduled()
    {
        var f = new SchedulingFixture();
        var appointment = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Appointments.StartVisitAsync(appointment.Id, f.Scheduler));
        await f.Appointments.CheckInAsync(appointment.Id, f.Scheduler);
        var started = await f.Appointments.StartVisitAsync(appointment.Id, f.Scheduler);
        Assert.Equal(AppointmentStatus.InProgress, started.Status);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Appointments.RescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(f.At(10), f.At(10, 30), null, null, null), f.Scheduler));

        var completed = await f.Appointments.CompleteAsync(appointment.Id, f.Scheduler);
        Assert.Equal(AppointmentStatus.Completed, completed.Status);
    }
}

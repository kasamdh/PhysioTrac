using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Role, portal-account, and tenant-ownership gates on the staff
/// AppointmentService: who can list/transition appointments, and that every
/// id a caller sends (therapist, provider, location, room, type) must belong
/// to the caller's own organization.</summary>
public class AppointmentSecurityTests
{
    private sealed record Seeded(
        PhysioTracDbContext Db, AppointmentService Service, Organization Org, Organization OtherOrg,
        Patient Taylor, Patient Riley, TestCurrentUser Scheduler, Guid TherapistId);

    private static async Task<Seeded> SeedAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Org 1000", Slug = "org-1000", ClientNumber = 1000 };
        var otherOrg = new Organization { Name = "Org 1001", Slug = "org-1001", ClientNumber = 1001 };
        var taylor = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Brooks", DateOfBirth = new DateOnly(1990, 1, 1), PortalUserId = Guid.NewGuid() };
        var riley = new Patient { OrganizationId = org.Id, FirstName = "Riley", LastName = "Simmons", DateOfBirth = new DateOnly(1991, 1, 1) };
        db.Organizations.AddRange(org, otherOrg);
        db.Patients.AddRange(taylor, riley);
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var service = new AppointmentService(db, new TenantAccessService(db, audit), audit, new RecordingReminderService());
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };
        return new Seeded(db, service, org, otherOrg, taylor, riley, scheduler, TestTherapists.Add(db, org.Id));
    }

    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(14);

    private static CreateAppointmentRequest Book(Guid patientId, Guid therapistId, DateTimeOffset start,
        Guid? providerId = null, Guid? locationId = null, Guid? roomId = null, Guid? appointmentTypeId = null) =>
        new(patientId, therapistId, providerId, locationId, roomId, appointmentTypeId, AppointmentKind.FollowUp, start, start.AddMinutes(30), false, null);

    private static TestCurrentUser PortalUser(Seeded s) =>
        new() { UserId = s.Taylor.PortalUserId!.Value, OrganizationId = s.Org.Id, Role = UserRole.Patient };

    // ---- S1: listing ----

    [Fact]
    public async Task ListForRange_PatientPortalAccount_SeesOnlyTheirOwnAppointments()
    {
        var s = await SeedAsync();
        var own = await s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start), s.Scheduler);
        await s.Service.CreateAsync(Book(s.Riley.Id, s.TherapistId, Start.AddHours(1)), s.Scheduler);

        var visible = await s.Service.ListForRangeAsync(PortalUser(s), Start.AddDays(-1), Start.AddDays(1));

        var only = Assert.Single(visible);
        Assert.Equal(own.Id, only.Id);
    }

    // ---- S2: status transitions ----

    [Fact]
    public async Task Cancel_ByPatientPortalAccount_IsForbidden_AndLeavesTheAppointmentAlone()
    {
        var s = await SeedAsync();
        var appointment = await s.Service.CreateAsync(Book(s.Riley.Id, s.TherapistId, Start), s.Scheduler);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CancelAsync(appointment.Id, PortalUser(s)));

        var unchanged = await s.Db.Appointments.FindAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.Scheduled, unchanged!.Status);
    }

    [Theory]
    [InlineData(UserRole.Biller)]
    [InlineData(UserRole.Compliance)]
    [InlineData(UserRole.Patient)]
    public async Task EveryStatusTransition_ByANonSchedulingRole_IsForbidden(UserRole role)
    {
        var s = await SeedAsync();
        var appointment = await s.Service.CreateAsync(Book(s.Riley.Id, s.TherapistId, Start), s.Scheduler);
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = s.Org.Id, Role = role };

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ConfirmAsync(appointment.Id, user));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CheckInAsync(appointment.Id, user));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CompleteAsync(appointment.Id, user));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkNoShowAsync(appointment.Id, user));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CancelAsync(appointment.Id, user));
    }

    [Fact]
    public async Task GetStatusHistory_ByPatientPortalAccount_ForAnotherPatientsAppointment_IsNotFound()
    {
        var s = await SeedAsync();
        var rileys = await s.Service.CreateAsync(Book(s.Riley.Id, s.TherapistId, Start), s.Scheduler);

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetStatusHistoryAsync(rileys.Id, PortalUser(s)));
    }

    // ---- S3: every referenced id must be the caller's own organization's ----

    [Fact]
    public async Task Create_WithAnotherOrganizationsTherapist_IsNotFound()
    {
        var s = await SeedAsync();
        var foreignTherapist = TestTherapists.Add(s.Db, s.OtherOrg.Id);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Book(s.Taylor.Id, foreignTherapist, Start), s.Scheduler));
        Assert.Contains("Therapist", ex.Message);
        Assert.Empty(s.Db.Appointments);
    }

    [Fact]
    public async Task Create_WithANonClinicianOrInactiveUserAsTherapist_IsNotFound()
    {
        var s = await SeedAsync();
        var biller = TestTherapists.Add(s.Db, s.Org.Id, UserRole.Biller);
        var suspended = TestTherapists.Add(s.Db, s.Org.Id);
        (await s.Db.Users.FindAsync(suspended))!.Status = UserStatus.Suspended;
        await s.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Book(s.Taylor.Id, biller, Start), s.Scheduler));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Book(s.Taylor.Id, suspended, Start), s.Scheduler));
    }

    [Fact]
    public async Task Create_WithAnotherOrganizationsLocationRoomOrType_IsNotFound()
    {
        var s = await SeedAsync();
        var foreignLocation = new Location { OrganizationId = s.OtherOrg.Id, Name = "Elsewhere" };
        var foreignRoom = new Room { LocationId = foreignLocation.Id, Name = "Gym" };
        var foreignType = new AppointmentType { OrganizationId = s.OtherOrg.Id, Name = "Eval" };
        s.Db.Locations.Add(foreignLocation);
        s.Db.Rooms.Add(foreignRoom);
        s.Db.AppointmentTypes.Add(foreignType);
        await s.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start, locationId: foreignLocation.Id), s.Scheduler));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start, roomId: foreignRoom.Id), s.Scheduler));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start, appointmentTypeId: foreignType.Id), s.Scheduler));
        Assert.Empty(s.Db.Appointments);
    }

    [Fact]
    public async Task Reschedule_ToAnotherOrganizationsProvider_IsNotFound_AndLeavesTheAppointmentAlone()
    {
        var s = await SeedAsync();
        var appointment = await s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start), s.Scheduler);
        var foreignProvider = new Provider { OrganizationId = s.OtherOrg.Id, FirstName = "Other", LastName = "Clinic", UserId = TestTherapists.Add(s.Db, s.OtherOrg.Id) };
        s.Db.Providers.Add(foreignProvider);
        await s.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.RescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(Start.AddHours(1), Start.AddHours(1).AddMinutes(30), foreignProvider.Id, null, null), s.Scheduler));

        var unchanged = await s.Db.Appointments.FindAsync(appointment.Id);
        Assert.Null(unchanged!.ProviderId);
        Assert.Equal(Start, unchanged.StartsAt);
    }

    // ---- S4: moving to another provider moves the clinician too ----

    private static async Task<(Provider Sarah, Provider Monika)> TwoProvidersAsync(Seeded s)
    {
        var sarah = new Provider { OrganizationId = s.Org.Id, FirstName = "Sarah", LastName = "Miller", UserId = s.TherapistId, Licenses = { TestTherapists.ValidLicense() } };
        var monika = new Provider { OrganizationId = s.Org.Id, FirstName = "Monika", LastName = "Pokhrel", UserId = TestTherapists.Add(s.Db, s.Org.Id), Licenses = { TestTherapists.ValidLicense() } };
        s.Db.Providers.AddRange(sarah, monika);
        await s.Db.SaveChangesAsync();
        return (sarah, monika);
    }

    [Fact]
    public async Task Reschedule_ToAnotherProvider_ReassignsTheTherapist_AndAuditsTheProviderChange()
    {
        var s = await SeedAsync();
        var (sarah, monika) = await TwoProvidersAsync(s);
        var appointment = await s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start, providerId: sarah.Id), s.Scheduler);

        var moved = await s.Service.RescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(Start, Start.AddMinutes(30), monika.Id, null, null), s.Scheduler);

        Assert.Equal(monika.Id, moved.ProviderId);
        Assert.Equal(monika.UserId, moved.TherapistId);
        Assert.True(await s.Db.AuditEvents.AnyAsync(e => e.ObjectId == appointment.Id && e.Action == "appointment.provider_changed"));

        var monikaView = new TestCurrentUser { UserId = monika.UserId!.Value, OrganizationId = s.Org.Id, Role = UserRole.Therapist };
        Assert.Single(await s.Service.ListForRangeAsync(monikaView, Start.AddDays(-1), Start.AddDays(1)));
    }

    [Fact]
    public async Task Reschedule_ToAnotherProvider_ChecksTheNewTherapistsCalendar_NotTheOldOne()
    {
        var s = await SeedAsync();
        var (sarah, monika) = await TwoProvidersAsync(s);
        var toMove = await s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start, providerId: sarah.Id), s.Scheduler);
        // Monika is already booked at 16:00 under her login, with no provider set --
        // only a therapist-level conflict check can catch this.
        await s.Service.CreateAsync(Book(s.Riley.Id, monika.UserId!.Value, Start.AddHours(2)), s.Scheduler);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => s.Service.RescheduleAsync(toMove.Id,
            new RescheduleAppointmentRequest(Start.AddHours(2), Start.AddHours(2).AddMinutes(30), monika.Id, null, null), s.Scheduler));
        Assert.Contains("therapist", ex.Message, StringComparison.OrdinalIgnoreCase);

        var unchanged = await s.Db.Appointments.FindAsync(toMove.Id);
        Assert.Equal(sarah.Id, unchanged!.ProviderId);
        Assert.Equal(s.TherapistId, unchanged.TherapistId);
    }

    [Fact]
    public async Task Reschedule_ToAProviderWithNoLoginAccount_IsRejected()
    {
        var s = await SeedAsync();
        var noLogin = new Provider { OrganizationId = s.Org.Id, FirstName = "No", LastName = "Login" };
        s.Db.Providers.Add(noLogin);
        await s.Db.SaveChangesAsync();
        var appointment = await s.Service.CreateAsync(Book(s.Taylor.Id, s.TherapistId, Start), s.Scheduler);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => s.Service.RescheduleAsync(appointment.Id,
            new RescheduleAppointmentRequest(Start, Start.AddMinutes(30), noLogin.Id, null, null), s.Scheduler));
    }
}

using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors the original's model-level double-booking backstop
/// (`Appointment.clean()`'s conflict query in `care/models.py`) and basic
/// tenant/role scoping for the staff-facing booking service.</summary>
public class AppointmentServiceTests
{
    private static (PhysioTracDbContext Db, AppointmentService Service, Organization Org, Patient Patient) NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new AppointmentService(db, tenantAccess, audit);
        return (db, service, org, patient);
    }

    private static TestCurrentUser Scheduler(Guid orgId) => new()
    {
        UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Scheduler,
    };

    [Fact]
    public async Task Create_OverlappingSlotForSameTherapist_IsRejected()
    {
        var (db, service, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var therapistId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        var first = new CreateAppointmentRequest(patient.Id, therapistId, null, null, null,
            AppointmentKind.FollowUp, start, start.AddMinutes(30), false, null);
        await service.CreateAsync(first, actor);

        var overlapping = new CreateAppointmentRequest(patient.Id, therapistId, null, null, null,
            AppointmentKind.FollowUp, start.AddMinutes(15), start.AddMinutes(45), false, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(overlapping, actor));
    }

    [Fact]
    public async Task Create_NonOverlappingSlotForSameTherapist_Succeeds()
    {
        var (db, service, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var therapistId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1).Date;

        var first = new CreateAppointmentRequest(patient.Id, therapistId, null, null, null,
            AppointmentKind.FollowUp, start, start.AddMinutes(30), false, null);
        await service.CreateAsync(first, actor);

        var second = new CreateAppointmentRequest(patient.Id, therapistId, null, null, null,
            AppointmentKind.FollowUp, start.AddMinutes(30), start.AddMinutes(60), false, null);
        var result = await service.CreateAsync(second, actor);

        Assert.Equal(AppointmentStatus.Scheduled, result.Status);
    }

    [Fact]
    public async Task Create_EndBeforeStart_Throws()
    {
        var (_, service, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var request = new CreateAppointmentRequest(patient.Id, Guid.NewGuid(), null, null, null,
            AppointmentKind.FollowUp, start, start.AddMinutes(-10), false, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, actor));
    }

    [Fact]
    public async Task Cancel_ScheduledAppointment_Succeeds()
    {
        var (_, service, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(new CreateAppointmentRequest(
            patient.Id, Guid.NewGuid(), null, null, null, AppointmentKind.FollowUp, start, start.AddMinutes(30), false, null), actor);

        var cancelled = await service.CancelAsync(created.Id, actor);

        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledAppointment_Throws()
    {
        var (_, service, org, patient) = NewService();
        var actor = Scheduler(org.Id);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateAsync(new CreateAppointmentRequest(
            patient.Id, Guid.NewGuid(), null, null, null, AppointmentKind.FollowUp, start, start.AddMinutes(30), false, null), actor);
        await service.CancelAsync(created.Id, actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(created.Id, actor));
    }

    [Fact]
    public async Task ListForRange_TherapistRole_OnlySeesOwnAppointments()
    {
        var (db, service, org, patient) = NewService();
        var scheduler = Scheduler(org.Id);
        var therapistAId = Guid.NewGuid();
        var therapistBId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);

        await service.CreateAsync(new CreateAppointmentRequest(
            patient.Id, therapistAId, null, null, null, AppointmentKind.FollowUp, start, start.AddMinutes(30), false, null), scheduler);
        await service.CreateAsync(new CreateAppointmentRequest(
            patient.Id, therapistBId, null, null, null, AppointmentKind.FollowUp, start.AddHours(2), start.AddHours(2).AddMinutes(30), false, null), scheduler);

        var therapistA = new TestCurrentUser { UserId = therapistAId, OrganizationId = org.Id, Role = UserRole.Therapist };
        var visible = await service.ListForRangeAsync(therapistA, start.AddDays(-1), start.AddDays(2));

        Assert.Single(visible);
        Assert.Equal(therapistAId, visible[0].TherapistId);
    }
}

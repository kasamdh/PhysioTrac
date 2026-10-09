using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Patient directory filters (location / status / appointment
/// dates) and the messaging inbox behind the React Administration pages.</summary>
public class AdministrationListsTests
{
    private sealed record Seed(
        PhysioTracDbContext Db, Organization Org, TestCurrentUser Admin,
        Location Fuquay, Location Raleigh, Patient Alpha, Patient Beta, Patient Gamma);

    private static async Task<Seed> SeedAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Org", Slug = "org", Timezone = "UTC" };
        var fuquay = new Location { OrganizationId = org.Id, Name = "Fuquay" };
        var raleigh = new Location { OrganizationId = org.Id, Name = "Raleigh" };
        var alpha = new Patient { OrganizationId = org.Id, FirstName = "Alpha", LastName = "A", DateOfBirth = new DateOnly(1980, 1, 1), PrimaryLocationId = fuquay.Id };
        var beta = new Patient { OrganizationId = org.Id, FirstName = "Beta", LastName = "B", DateOfBirth = new DateOnly(1981, 1, 1), PrimaryLocationId = raleigh.Id };
        var gamma = new Patient { OrganizationId = org.Id, FirstName = "Gamma", LastName = "C", DateOfBirth = new DateOnly(1982, 1, 1), Status = PatientStatus.Discharged };
        db.Organizations.Add(org);
        db.Locations.AddRange(fuquay, raleigh);
        db.Patients.AddRange(alpha, beta, gamma);
        await db.SaveChangesAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        return new Seed(db, org, admin, fuquay, raleigh, alpha, beta, gamma);
    }

    private static void AddAppointment(Seed s, Patient patient, Location location, DateTimeOffset startsAt,
        AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        s.Db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id,
            TherapistId = Guid.NewGuid(),
            CreatedById = s.Admin.UserId,
            LocationDetailId = location.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(1),
            Status = status,
        });
    }

    private static PatientsController NewPatientsController(Seed s)
    {
        var audit = new AuditService(s.Db);
        var controller = new PatientsController(new TenantAccessService(s.Db, audit), s.Admin, audit, s.Db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<PagedPatientDirectoryDto> DirectoryAsync(Seed s, Guid? locationId = null,
        PatientStatus? status = null, DateOnly? from = null, DateOnly? to = null)
    {
        var result = Assert.IsType<OkObjectResult>(await NewPatientsController(s).Directory(
            status: status, locationId: locationId, appointmentFrom: from, appointmentTo: to));
        return Assert.IsType<PagedPatientDirectoryDto>(result.Value);
    }

    [Fact]
    public async Task Directory_Location_MatchesPrimaryLocationOrAnyAppointmentThere()
    {
        var s = await SeedAsync();
        AddAppointment(s, s.Gamma, s.Fuquay, new DateTimeOffset(2026, 3, 2, 15, 0, 0, TimeSpan.Zero));
        await s.Db.SaveChangesAsync();

        var page = await DirectoryAsync(s, locationId: s.Fuquay.Id);

        Assert.Equal(new[] { s.Alpha.Id, s.Gamma.Id }, page.Items.Select(i => i.Id));
        Assert.Equal("Fuquay", page.Items[0].PrimaryLocationName);
    }

    [Fact]
    public async Task Directory_Status_FiltersByPatientStatus()
    {
        var s = await SeedAsync();

        var page = await DirectoryAsync(s, status: PatientStatus.Discharged);

        Assert.Equal(s.Gamma.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Directory_AppointmentDates_AreInclusiveAndIgnoreCancelled()
    {
        var s = await SeedAsync();
        AddAppointment(s, s.Alpha, s.Fuquay, new DateTimeOffset(2026, 3, 10, 23, 30, 0, TimeSpan.Zero)); // last minute of the range
        AddAppointment(s, s.Beta, s.Raleigh, new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero), AppointmentStatus.Cancelled);
        AddAppointment(s, s.Gamma, s.Raleigh, new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero)); // day after the range
        await s.Db.SaveChangesAsync();

        var page = await DirectoryAsync(s, from: new DateOnly(2026, 3, 1), to: new DateOnly(2026, 3, 10));

        Assert.Equal(s.Alpha.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Directory_LocationWithDates_RequiresTheAppointmentAtThatLocation()
    {
        var s = await SeedAsync();
        // Alpha's primary location is Fuquay, but the in-range visit is at Raleigh.
        AddAppointment(s, s.Alpha, s.Raleigh, new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero));
        AddAppointment(s, s.Beta, s.Fuquay, new DateTimeOffset(2026, 3, 6, 9, 0, 0, TimeSpan.Zero));
        await s.Db.SaveChangesAsync();

        var page = await DirectoryAsync(s, locationId: s.Fuquay.Id, from: new DateOnly(2026, 3, 1), to: new DateOnly(2026, 3, 31));

        Assert.Equal(s.Beta.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Directory_ReportsLastVisitAndNextAppointment()
    {
        var s = await SeedAsync();
        var past = DateTimeOffset.UtcNow.AddDays(-3);
        var future = DateTimeOffset.UtcNow.AddDays(4);
        AddAppointment(s, s.Alpha, s.Fuquay, past.AddDays(-7));
        AddAppointment(s, s.Alpha, s.Fuquay, past);
        AddAppointment(s, s.Alpha, s.Fuquay, future);
        AddAppointment(s, s.Alpha, s.Fuquay, future.AddDays(-1), AppointmentStatus.Cancelled);
        await s.Db.SaveChangesAsync();

        var alpha = (await DirectoryAsync(s)).Items.Single(i => i.Id == s.Alpha.Id);

        Assert.Equal(past, alpha.LastVisitAt);
        Assert.Equal(future, alpha.NextAppointmentAt);
    }

    [Fact]
    public async Task ListThreads_GroupsByPatient_NewestFirst_CountsUnreadFromOthers()
    {
        var s = await SeedAsync();
        var colleague = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        s.Db.Messages.AddRange(
            new Message { OrganizationId = s.Org.Id, PatientId = s.Alpha.Id, SenderId = colleague, Body = "first", SentAt = t0 },
            new Message { OrganizationId = s.Org.Id, PatientId = s.Alpha.Id, SenderId = s.Admin.UserId, Body = "mine", SentAt = t0.AddHours(1) },
            new Message { OrganizationId = s.Org.Id, PatientId = s.Beta.Id, SenderId = colleague, Body = "newest", SentAt = t0.AddDays(1), ReadAt = t0.AddDays(1) });
        await s.Db.SaveChangesAsync();
        var audit = new AuditService(s.Db);
        var service = new MessageService(s.Db, new TenantAccessService(s.Db, audit), audit);

        var threads = await service.ListThreadsAsync(s.Admin);

        Assert.Equal(new[] { s.Beta.Id, s.Alpha.Id }, threads.Select(t => t.PatientId));
        var alpha = threads[1];
        Assert.Equal("mine", alpha.LastMessagePreview);
        Assert.Equal(2, alpha.MessageCount);
        Assert.Equal(1, alpha.UnreadCount); // own message doesn't count as unread
        Assert.Equal(0, threads[0].UnreadCount);
    }

    [Fact]
    public async Task ListThreads_ExcludesOtherOrganizationsPatients()
    {
        var s = await SeedAsync();
        var otherOrg = new Organization { Name = "Other", Slug = "other" };
        var outsider = new Patient { OrganizationId = otherOrg.Id, FirstName = "Out", LastName = "Sider", DateOfBirth = new DateOnly(1990, 1, 1) };
        s.Db.Organizations.Add(otherOrg);
        s.Db.Patients.Add(outsider);
        s.Db.Messages.Add(new Message { OrganizationId = otherOrg.Id, PatientId = outsider.Id, SenderId = Guid.NewGuid(), Body = "x" });
        await s.Db.SaveChangesAsync();
        var audit = new AuditService(s.Db);
        var service = new MessageService(s.Db, new TenantAccessService(s.Db, audit), audit);

        Assert.Empty(await service.ListThreadsAsync(s.Admin));
    }
}

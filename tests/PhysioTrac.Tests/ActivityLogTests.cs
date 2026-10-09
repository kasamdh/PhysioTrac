using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Auditing;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Administration › Logs: wider automatic change logging, the log
/// list endpoint, and screen ("page view") logging.</summary>
public class ActivityLogTests
{
    private static (PhysioTracDbContext Db, Organization Org, Patient Pat, TestCurrentUser Admin) Setup(TestCurrentUser? actor = null)
    {
        var org = new Organization { Name = "Org", Slug = "org", Timezone = "UTC" };
        var admin = actor ?? new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        admin.OrganizationId ??= org.Id;
        var db = new PhysioTracDbContext(new DbContextOptionsBuilder<PhysioTracDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new EntityChangeAuditInterceptor(admin))
            .Options);
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Quinn", LastName = "Alvarez", DateOfBirth = new DateOnly(2001, 7, 30), MedicalRecordNumber = "SM-1" };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        return (db, org, pat, admin);
    }

    private static AuditLogsController Controller(PhysioTracDbContext db, TestCurrentUser actor)
    {
        var audit = new AuditService(db);
        return new AuditLogsController(new TenantAccessService(db, audit), actor, audit, db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static async Task<AuditLogPageDto> ListAsync(PhysioTracDbContext db, TestCurrentUser actor,
        string? category = null, string? search = null, Guid? userId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = Assert.IsType<OkObjectResult>(await Controller(db, actor).List(today, today, userId, category, search));
        return Assert.IsType<AuditLogPageDto>(result.Value);
    }

    [Fact]
    public async Task AnAppointmentChange_IsNowLogged_UnderTheUsersOrganization_WithThePatient()
    {
        var (db, org, pat, admin) = Setup();
        var appt = new Appointment
        {
            PatientId = pat.Id,
            TherapistId = Guid.NewGuid(),
            CreatedById = admin.UserId,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(30),
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();
        appt.Status = AppointmentStatus.CheckedIn;
        await db.SaveChangesAsync();

        var events = await db.AuditEvents.Where(a => a.ObjectType == nameof(Appointment)).ToListAsync();
        Assert.Equal(new[] { "entity.created", "entity.updated" }, events.OrderBy(e => e.CreatedAt).Select(e => e.Action));
        Assert.All(events, e =>
        {
            Assert.Equal(org.Id, e.OrganizationId);
            Assert.Equal(pat.Id, e.PatientId);
            Assert.Equal(admin.UserId, e.ActorId);
        });
    }

    [Fact]
    public async Task SessionsAndInternalHistoryRows_AreNotLogged()
    {
        var (db, org, _, admin) = Setup();
        db.UserSessions.Add(new UserSession { UserId = admin.UserId, OrganizationId = org.Id, SessionKey = "k", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        db.AppointmentStatusHistories.Add(new AppointmentStatusHistory { AppointmentId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        Assert.False(await db.AuditEvents.AnyAsync(a => a.ObjectType == nameof(UserSession) || a.ObjectType == nameof(AppointmentStatusHistory)));
    }

    [Fact]
    public async Task ChangesWithoutASignedInUser_ToRecordsWithoutTheirOwnOrganization_AreSkipped()
    {
        var system = new TestCurrentUser { IsAuthenticated = false };
        var (db, _, pat, _) = Setup(system);
        db.Appointments.Add(new Appointment { PatientId = pat.Id, TherapistId = Guid.NewGuid(), StartsAt = DateTimeOffset.UtcNow, EndsAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        Assert.False(await db.AuditEvents.AnyAsync(a => a.ObjectType == nameof(Appointment)));
    }

    [Fact]
    public async Task List_DescribesEntries_ResolvesUserAndPatient_AndFiltersByCategoryAndSearch()
    {
        var (db, org, pat, admin) = Setup();
        db.Users.Add(new PhysioTrac.Infrastructure.Identity.ApplicationUser { Id = admin.UserId, UserName = "admin", FirstName = "Alex", LastName = "Rivera", OrganizationId = org.Id });
        db.AuditEvents.AddRange(
            new AuditEvent { OrganizationId = org.Id, ActorId = admin.UserId, Action = "auth.login.success", ObjectType = "ApplicationUser" },
            new AuditEvent { OrganizationId = org.Id, ActorId = admin.UserId, Action = "appointment.checked_in", ObjectType = "Appointment", PatientId = pat.Id },
            new AuditEvent { OrganizationId = org.Id, ActorId = admin.UserId, Action = "page.viewed", ObjectType = "Page", MetadataJson = "{\"path\":\"/admin/users\"}" },
            new AuditEvent { OrganizationId = Guid.NewGuid(), ActorId = admin.UserId, Action = "auth.login.success", ObjectType = "ApplicationUser" });
        await db.SaveChangesAsync();

        var all = await ListAsync(db, admin);
        Assert.DoesNotContain(all.Items, i => i.Action == "entity.created" && i.ObjectType == nameof(UserSession));
        var checkIn = all.Items.Single(i => i.Action == "appointment.checked_in");
        Assert.Equal("Checked a patient in", checkIn.Description);
        Assert.Equal("Alex Rivera", checkIn.UserName);
        Assert.Equal("Quinn Alvarez", checkIn.PatientName);
        Assert.Equal(AuditCategories.Schedule, checkIn.Category);
        Assert.Equal("Opened Admin › Users", all.Items.Single(i => i.Action == "page.viewed").Description);
        Assert.Equal(1, all.Items.Count(i => i.Action == "auth.login.success")); // the other org's sign-in is excluded

        Assert.Equal("auth.login.success", Assert.Single((await ListAsync(db, admin, category: AuditCategories.SignIn)).Items).Action);
        var forPatient = (await ListAsync(db, admin, search: "SM-1")).Items; // by MRN: chart created + check-in
        Assert.Contains(forPatient, i => i.Action == "appointment.checked_in");
        Assert.All(forPatient, i => Assert.Equal(pat.Id, i.PatientId));
        Assert.Equal("appointment.checked_in", Assert.Single((await ListAsync(db, admin, search: "checked a patient")).Items).Action);
    }

    [Fact]
    public async Task List_IsForAdminsDirectorsAndCompliance_Only()
    {
        var (db, org, _, _) = Setup();
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var compliance = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Compliance };

        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller(db, therapist).List()).StatusCode);
        Assert.IsType<OkObjectResult>(await Controller(db, compliance).List());
    }

    [Fact]
    public async Task List_RejectsRangesLongerThanTheLimit()
    {
        var (db, _, _, admin) = Setup();
        var result = await Controller(db, admin).List(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));
        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    [Fact]
    public async Task PageView_RecordsThePathOnly_AndRejectsAnythingElse()
    {
        var (db, org, _, admin) = Setup();
        var controller = Controller(db, admin);

        Assert.IsType<NoContentResult>(await controller.PageView(new PageViewRequest("/patients?q=Quinn")));
        Assert.IsType<BadRequestObjectResult>(await controller.PageView(new PageViewRequest("https://evil.example")));
        Assert.IsType<BadRequestObjectResult>(await controller.PageView(new PageViewRequest("")));

        var view = await db.AuditEvents.SingleAsync(a => a.Action == "page.viewed");
        Assert.Equal(org.Id, view.OrganizationId);
        Assert.Equal("/patients", JsonDocument.Parse(view.MetadataJson).RootElement.GetProperty("path").GetString());
        Assert.DoesNotContain("Quinn", view.MetadataJson);
    }
}

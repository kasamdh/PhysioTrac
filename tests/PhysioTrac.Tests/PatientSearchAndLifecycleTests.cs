using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Patients;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class PatientSearchAndLifecycleTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PatientsController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var controller = new PatientsController(tenantAccess, user, audit, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<(PhysioTracDbContext Db, Organization Org, TestCurrentUser Admin, Patient Alpha, Patient Beta)> SeedAsync()
    {
        var db = NewDb();
        var org = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var alpha = new Patient { OrganizationId = org.Id, FirstName = "Alpha", LastName = "Anderson", DateOfBirth = new DateOnly(1980, 1, 1), Status = PatientStatus.Active };
        var beta = new Patient { OrganizationId = org.Id, FirstName = "Beta", LastName = "Brown", DateOfBirth = new DateOnly(1990, 1, 1), Status = PatientStatus.Discharged };
        db.Organizations.Add(org);
        db.Patients.AddRange(alpha, beta);
        await db.SaveChangesAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        return (db, org, admin, alpha, beta);
    }

    [Fact]
    public async Task List_SearchByLastName_FiltersToMatchingPatientsOnly()
    {
        var (db, _, admin, alpha, _) = await SeedAsync();
        var controller = NewController(db, admin);

        var result = Assert.IsType<OkObjectResult>(await controller.List(search: "Anderson"));
        var page = Assert.IsType<PagedPatientsDto>(result.Value);

        var only = Assert.Single(page.Items);
        Assert.Equal(alpha.Id, only.Id);
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsOnlyThatStatus()
    {
        var (db, _, admin, _, beta) = await SeedAsync();
        var controller = NewController(db, admin);

        var result = Assert.IsType<OkObjectResult>(await controller.List(status: PatientStatus.Discharged));
        var page = Assert.IsType<PagedPatientsDto>(result.Value);

        var only = Assert.Single(page.Items);
        Assert.Equal(beta.Id, only.Id);
    }

    [Fact]
    public async Task List_Pagination_ReturnsCorrectPageAndTotal()
    {
        var (db, org, admin, _, _) = await SeedAsync();
        for (var i = 0; i < 8; i++)
        {
            db.Patients.Add(new Patient { OrganizationId = org.Id, FirstName = $"P{i}", LastName = "Extra", DateOfBirth = new DateOnly(2000, 1, 1) });
        }
        await db.SaveChangesAsync();
        var controller = NewController(db, admin);

        var result = Assert.IsType<OkObjectResult>(await controller.List(page: 2, pageSize: 5));
        var page = Assert.IsType<PagedPatientsDto>(result.Value);

        Assert.Equal(10, page.Total); // 2 seeded + 8 extra
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(2, page.Page);
    }

    [Fact]
    public async Task Get_OpeningAChart_WritesAPatientViewedAuditEvent()
    {
        var (db, org, admin, alpha, _) = await SeedAsync();
        var controller = NewController(db, admin);

        await controller.Get(alpha.Id);

        var viewEvents = await db.AuditEvents.Where(e => e.ObjectId == alpha.Id && e.Action == "patient.viewed").ToListAsync();
        var viewEvent = Assert.Single(viewEvents);
        Assert.Equal(admin.UserId, viewEvent.ActorId);
        Assert.Equal(org.Id, viewEvent.OrganizationId);
    }

    [Fact]
    public async Task Delete_SoftDeletes_ExcludesFromListAndGet_ButPreservesTheRow()
    {
        var (db, _, admin, alpha, _) = await SeedAsync();
        var controller = NewController(db, admin);

        Assert.IsType<NoContentResult>(await controller.Delete(alpha.Id));

        var listResult = Assert.IsType<OkObjectResult>(await controller.List());
        var page = Assert.IsType<PagedPatientsDto>(listResult.Value);
        Assert.DoesNotContain(page.Items, p => p.Id == alpha.Id);

        var getResult = await controller.Get(alpha.Id);
        Assert.IsType<ObjectResult>(getResult); // 403 -- PatientsFor excludes it, so it reads as inaccessible, not silently gone

        var stillInDatabase = await db.Patients.FindAsync(alpha.Id);
        Assert.NotNull(stillInDatabase);
        Assert.True(stillInDatabase!.IsDeleted);
    }

    [Fact]
    public async Task Restore_UndeletesAPatient_MakingThemVisibleAgain()
    {
        var (db, _, admin, alpha, _) = await SeedAsync();
        var controller = NewController(db, admin);
        await controller.Delete(alpha.Id);

        var result = Assert.IsType<OkObjectResult>(await controller.Restore(alpha.Id));
        var dto = Assert.IsType<PatientDetailDto>(result.Value);
        Assert.Equal(alpha.Id, dto.Id);

        var getResult = Assert.IsType<OkObjectResult>(await controller.Get(alpha.Id));
        Assert.IsType<PatientDetailDto>(getResult.Value);
    }

    [Fact]
    public async Task Delete_ByUnauthorizedRole_Returns403_AndLeavesThePatientAlone()
    {
        var (db, org, _, alpha, _) = await SeedAsync();
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var controller = NewController(db, therapist);

        var result = Assert.IsType<ObjectResult>(await controller.Delete(alpha.Id));
        Assert.Equal(403, result.StatusCode);

        var unchanged = await db.Patients.FindAsync(alpha.Id);
        Assert.False(unchanged!.IsDeleted);
    }

    [Fact]
    public async Task Restore_OnAnotherOrganizationsPatient_ThrowsNotFound()
    {
        var (db, _, admin, alpha, _) = await SeedAsync();
        var otherOrg = new Organization { Name = "Org 1001", Slug = "org-1001" };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        alpha.OrganizationId = otherOrg.Id;
        await db.SaveChangesAsync();

        var controller = NewController(db, admin);
        var result = await controller.Restore(alpha.Id);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Timeline_ReturnsEmptyArraysForModulesNotBuiltYet_AndRealDataForOthers()
    {
        var (db, org, admin, alpha, _) = await SeedAsync();
        db.PatientDocuments.Add(new PatientDocument
        {
            OrganizationId = org.Id, PatientId = alpha.Id, UploadedById = admin.UserId,
            OriginalFilename = "eval.pdf", ContentType = "application/pdf", StorageKey = "abc123",
        });
        await db.SaveChangesAsync();

        var controller = NewController(db, admin);
        var result = Assert.IsType<OkObjectResult>(await controller.Timeline(alpha.Id));
        var timeline = Assert.IsType<PatientTimelineDto>(result.Value);

        Assert.Empty(timeline.Forms);
        Assert.Single(timeline.Documents);
        Assert.Empty(timeline.Appointments);
    }
}

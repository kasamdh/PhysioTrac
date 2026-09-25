using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

public class HomeExerciseProgramServiceTests
{
    private static (PhysioTracDbContext Db, HomeExerciseProgramService Service, Organization Org, Patient Patient, TestCurrentUser Therapist)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var therapistId = Guid.NewGuid();
        var patient = new Patient
        {
            OrganizationId = org.Id,
            FirstName = "Pat",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            AssignedTherapistId = therapistId,
        };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var therapist = new TestCurrentUser { UserId = therapistId, OrganizationId = org.Id, Role = UserRole.Therapist };

        return (db, new HomeExerciseProgramService(db, tenantAccess, audit), org, patient, therapist);
    }

    private static CreateHomeExerciseProgramRequest ValidRequest(Guid patientId) => new(
        patientId, "Phase 1 - Post-op Knee", "Perform daily, stop if pain exceeds 5/10.",
        new List<CreateHomeExerciseItemRequest>
        {
            new("Quad sets", "Tighten thigh muscle, hold, release", 3, 10, 5, 2, null, null),
            new("Heel slides", null, 3, 10, null, 2, null, null),
        });

    [Fact]
    public async Task Create_ValidRequest_PersistsProgramWithOrderedItems()
    {
        var (db, service, _, patient, therapist) = NewService();

        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);

        Assert.Single(await db.HomeExercisePrograms.ToListAsync());
        Assert.Equal(2, program.Items.Count);
        Assert.Equal(0, program.Items.ElementAt(0).Order);
        Assert.Equal(1, program.Items.ElementAt(1).Order);
        Assert.Equal(HomeExerciseProgramStatus.Active, program.Status);
    }

    [Fact]
    public async Task Create_BlankTitle_Throws()
    {
        var (_, service, _, patient, therapist) = NewService();
        var request = ValidRequest(patient.Id) with { Title = "  " };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, therapist));
    }

    [Fact]
    public async Task Create_SchedulerCannotCreate()
    {
        var (_, service, org, patient, _) = NewService();
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(ValidRequest(patient.Id), scheduler));
    }

    [Fact]
    public async Task AddItem_ToActiveProgram_AppendsAtNextOrder()
    {
        var (_, service, _, patient, therapist) = NewService();
        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);

        var newItem = await service.AddItemAsync(program.Id, new CreateHomeExerciseItemRequest("Ankle pumps", null, null, 20, null, 3, null, null), therapist);

        Assert.Equal(2, newItem.Order);
    }

    [Fact]
    public async Task AddItem_ToDiscontinuedProgram_Throws()
    {
        var (_, service, _, patient, therapist) = NewService();
        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);
        await service.DiscontinueAsync(program.Id, therapist);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddItemAsync(program.Id, new CreateHomeExerciseItemRequest("Ankle pumps", null, null, null, null, null, null, null), therapist));
    }

    [Fact]
    public async Task RemoveItem_ExistingItem_Removes()
    {
        var (db, service, _, patient, therapist) = NewService();
        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);
        var itemId = program.Items.First().Id;

        await service.RemoveItemAsync(program.Id, itemId, therapist);

        var remaining = await db.HomeExerciseItems.Where(i => i.ProgramId == program.Id).ToListAsync();
        Assert.Single(remaining);
    }

    [Fact]
    public async Task Discontinue_ActiveProgram_Succeeds()
    {
        var (_, service, _, patient, therapist) = NewService();
        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);

        var discontinued = await service.DiscontinueAsync(program.Id, therapist);

        Assert.Equal(HomeExerciseProgramStatus.Discontinued, discontinued.Status);
        Assert.NotNull(discontinued.DiscontinuedAt);
    }

    [Fact]
    public async Task Discontinue_AlreadyDiscontinued_Throws()
    {
        var (_, service, _, patient, therapist) = NewService();
        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);
        await service.DiscontinueAsync(program.Id, therapist);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DiscontinueAsync(program.Id, therapist));
    }

    [Fact]
    public async Task ListForPatient_OtherTherapistsPatient_IsForbidden()
    {
        var (_, service, org, patient, _) = NewService();
        var otherTherapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListForPatientAsync(patient.Id, otherTherapist));
    }

    [Fact]
    public async Task Discontinue_ForAnotherOrganizationsProgram_ThrowsNotFound()
    {
        var (db, service, _, patient, therapist) = NewService();
        var program = await service.CreateAsync(ValidRequest(patient.Id), therapist);
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgTherapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = otherOrg.Id, Role = UserRole.Therapist };

        await Assert.ThrowsAsync<NotFoundException>(() => service.DiscontinueAsync(program.Id, otherOrgTherapist));
    }
}

using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

public class FunctionalGoalAndOutcomeTests
{
    private static (PhysioTracDbContext Db, FunctionalGoalService Goals, OutcomeScoreService Outcomes, Organization Org, Patient Patient, TestCurrentUser Therapist) NewServices()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var therapistId = Guid.NewGuid();
        // A Therapist-role caller only sees their own assigned caseload
        // (the same narrowing TenantAccessServiceTests verifies in Module 1)
        // — assign this patient to the therapist so these CRUD tests aren't
        // blocked by that unrelated scoping rule.
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1), AssignedTherapistId = therapistId };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var therapist = new TestCurrentUser { UserId = therapistId, OrganizationId = org.Id, Role = UserRole.Therapist };
        return (db, new FunctionalGoalService(db, tenantAccess, audit), new OutcomeScoreService(db, tenantAccess, audit), org, patient, therapist);
    }

    [Fact]
    public async Task Goal_StartsAsDraft_AndBecomesActiveOnlyAfterApproval()
    {
        var (_, goals, _, _, patient, therapist) = NewServices();

        var goal = await goals.CreateAsync(new CreateGoalRequest(
            patient.Id, "Difficulty climbing stairs", "Climb 12 stairs without rail", GoalTerm.ShortTerm, 3, 12, "stairs", "Direct observation",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), "Patient will climb 12 stairs independently."), therapist);

        Assert.Equal(GoalStatus.Draft, goal.Status);

        var approved = await goals.ApproveAsync(goal.Id, therapist);
        Assert.Equal(GoalStatus.Active, approved.Status);
        Assert.Equal(therapist.UserId, approved.ApprovedById);
    }

    [Fact]
    public async Task Goal_ProgressPercent_ComputesFromBaselineAndTarget()
    {
        var (_, goals, _, _, patient, therapist) = NewServices();
        var goal = await goals.CreateAsync(new CreateGoalRequest(
            patient.Id, "Limited reach", "Reach overhead shelf", GoalTerm.LongTerm, 0, 10, "reps", "Direct observation",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), "wording"), therapist);

        var updated = await goals.UpdateProgressAsync(goal.Id, new UpdateGoalProgressRequest(5), therapist);

        Assert.Equal(50, updated.ProgressPercent);
    }

    [Fact]
    public async Task Goal_ApproveForAnotherOrganizationsGoal_ThrowsNotFound()
    {
        var (db, goals, _, org, patient, therapist) = NewServices();
        var goal = await goals.CreateAsync(new CreateGoalRequest(
            patient.Id, "Difficulty climbing stairs", "Climb 12 stairs without rail", GoalTerm.ShortTerm, 3, 12, "stairs", "Direct observation",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), "Patient will climb 12 stairs independently."), therapist);
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgTherapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = otherOrg.Id, Role = UserRole.Therapist };

        await Assert.ThrowsAsync<NotFoundException>(() => goals.ApproveAsync(goal.Id, otherOrgTherapist));
    }

    [Fact]
    public async Task OutcomeScore_AboveMaximum_IsRejected()
    {
        var (_, _, outcomes, _, patient, therapist) = NewServices();

        await Assert.ThrowsAsync<InvalidOperationException>(() => outcomes.RecordAsync(
            new RecordOutcomeScoreRequest(patient.Id, null, OutcomeMeasure.Lefs, DateOnly.FromDateTime(DateTime.UtcNow), 85, 80, null), therapist));
    }

    [Fact]
    public async Task OutcomeScore_SameDaySameMeasure_UpdatesExistingRow()
    {
        var (db, _, outcomes, _, patient, therapist) = NewServices();
        var day = DateOnly.FromDateTime(DateTime.UtcNow);

        await outcomes.RecordAsync(new RecordOutcomeScoreRequest(patient.Id, null, OutcomeMeasure.Lefs, day, 40, 80, null), therapist);
        await outcomes.RecordAsync(new RecordOutcomeScoreRequest(patient.Id, null, OutcomeMeasure.Lefs, day, 55, 80, null), therapist);

        var rows = await db.OutcomeScores.Where(o => o.PatientId == patient.Id).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(55, rows[0].Score);
    }
}

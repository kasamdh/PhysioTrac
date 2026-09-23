using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Messaging;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

public class MessageServiceTests
{
    private static (PhysioTracDbContext Db, MessageService Service, Organization Org, Patient Patient, TestCurrentUser FrontDesk)
        NewService()
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
        var frontDesk = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };

        return (db, new MessageService(db, tenantAccess, audit), org, patient, frontDesk);
    }

    [Fact]
    public async Task Send_ValidMessage_Persists()
    {
        var (db, service, _, patient, actor) = NewService();

        var message = await service.SendAsync(new SendMessageRequest(patient.Id, "Your appointment is confirmed for Tuesday."), actor);

        Assert.Single(await db.Messages.ToListAsync());
        Assert.Equal(UserRole.Scheduler, message.SenderRole);
        Assert.False(message.IsFromPatient);
        Assert.Null(message.ReadAt);
    }

    [Fact]
    public async Task Send_BlankBody_Throws()
    {
        var (_, service, _, patient, actor) = NewService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(new SendMessageRequest(patient.Id, "   "), actor));
    }

    [Fact]
    public async Task Send_PatientRoleCannotSendOnOwnBehalfYet()
    {
        var (_, service, _, patient, _) = NewService();
        var patientActor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SendAsync(new SendMessageRequest(patient.Id, "Hello"), patientActor));
    }

    [Fact]
    public async Task ListForPatient_ReturnsInSentOrder()
    {
        var (_, service, _, patient, actor) = NewService();
        await service.SendAsync(new SendMessageRequest(patient.Id, "First"), actor);
        await service.SendAsync(new SendMessageRequest(patient.Id, "Second"), actor);

        var thread = await service.ListForPatientAsync(patient.Id, actor);

        Assert.Equal(2, thread.Count);
        Assert.Equal("First", thread[0].Body);
        Assert.Equal("Second", thread[1].Body);
    }

    [Fact]
    public async Task MarkThreadRead_OnlyMarksOthersMessages_NotOwnSent()
    {
        var (db, service, org, patient, actor) = NewService();
        var otherStaff = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };

        await service.SendAsync(new SendMessageRequest(patient.Id, "From front desk"), actor);
        await service.SendAsync(new SendMessageRequest(patient.Id, "From billing"), otherStaff);

        await service.MarkThreadReadAsync(patient.Id, actor);

        var all = await db.Messages.OrderBy(m => m.SentAt).ToListAsync();
        Assert.Null(all[0].ReadAt); // actor's own message stays unread-by-self
        Assert.NotNull(all[1].ReadAt); // the other staff member's message was marked read
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.SuperAdmin;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors the intent of the original `client_management.py`
/// behavior: unique slugs, monotonic client numbers starting at 1000, an
/// admin created with no usable password until invitation activation, and
/// full audit coverage of the provisioning + lifecycle actions.</summary>
public class ClientProvisioningServiceTests
{
    private static (PhysioTracDbContext Db, ClientProvisioningService Service, UserManager<ApplicationUser> Users) NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, PhysioTracDbContext, Guid>(db);
        var userManager = new UserManager<ApplicationUser>(
            userStore, Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var audit = new AuditService(db);
        var service = new ClientProvisioningService(db, userManager, audit, Options.Create(new AppOptions()));
        return (db, service, userManager);
    }

    private static ProvisionClientRequest SampleRequest(string name = "Riverside PT") => new(
        ClientName: name, ClientEmail: "billing@riverside.example", ClientPhone: "555-0100",
        AddressLine1: "1 Main St", AddressLine2: null, City: "Springfield", State: "IL", ZipCode: "62701",
        Country: "United States", SubscriptionTier: SubscriptionTier.Professional, Timezone: "America/Chicago",
        Comments: null, AdminFirstName: "Ada", AdminLastName: "Admin",
        AdminEmail: $"ada-{Slugify(name)}@riverside.example", LocationName: "Main Clinic");

    private static string Slugify(string name) => name.ToLowerInvariant().Replace(" ", "-");

    private static TestCurrentUser PlatformSuperAdmin() => new()
    {
        UserId = Guid.NewGuid(), OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true,
    };

    [Fact]
    public async Task ProvisionClient_CreatesOrgAndAdminWithNoUsablePassword()
    {
        var (db, service, users) = NewService();
        var actor = PlatformSuperAdmin();

        var result = await service.ProvisionClientAsync(SampleRequest(), actor);

        Assert.Equal(1000, result.Client.ClientNumber);
        Assert.False(string.IsNullOrEmpty(result.Client.Slug));

        var admin = await users.FindByIdAsync(result.AdministratorId.ToString());
        Assert.NotNull(admin);
        Assert.False(await users.HasPasswordAsync(admin!));
        Assert.Equal(UserRole.Admin, admin!.Role);
    }

    [Fact]
    public async Task ProvisionClient_AssignsSequentialClientNumbers()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();

        var first = await service.ProvisionClientAsync(SampleRequest("Client One"), actor);
        var second = await service.ProvisionClientAsync(SampleRequest("Client Two"), actor);

        Assert.Equal(1000, first.Client.ClientNumber);
        Assert.Equal(1001, second.Client.ClientNumber);
    }

    [Fact]
    public async Task ProvisionClient_DuplicateName_GetsSuffixedSlug()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();

        var first = await service.ProvisionClientAsync(SampleRequest("Same Name"), actor);
        var second = await service.ProvisionClientAsync(new ProvisionClientRequest(
            "Same Name", "b@x.example", null, "1 St", null, "City", "ST", "00000", null,
            SubscriptionTier.Starter, "America/Chicago", null, "A", "B", "b-admin@x.example", "Main Clinic"), actor);

        Assert.NotEqual(first.Client.Slug, second.Client.Slug);
        Assert.StartsWith(first.Client.Slug, second.Client.Slug);
    }

    [Fact]
    public async Task SuspendThenActivate_RoundTrips()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), actor);

        var suspended = await service.SuspendClientAsync(provisioned.Client.ClientNumber!.Value, "non-payment", actor);
        Assert.Equal(OrganizationStatus.Suspended, suspended.Status);

        var reactivated = await service.ActivateClientAsync(provisioned.Client.ClientNumber!.Value, actor);
        Assert.Equal(OrganizationStatus.Active, reactivated.Status);
        Assert.Null(reactivated.SuspendedAt);
    }

    [Fact]
    public async Task SuspendingAlreadySuspendedClient_Throws()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), actor);
        await service.SuspendClientAsync(provisioned.Client.ClientNumber!.Value, "reason", actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SuspendClientAsync(provisioned.Client.ClientNumber!.Value, "reason again", actor));
    }

    [Fact]
    public async Task ProvisionClient_StartsInTrial_WithATrialEndDateSet()
    {
        var (_, service, _) = NewService();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), PlatformSuperAdmin());

        Assert.Equal(OrganizationStatus.Trial, provisioned.Client.Status);
        Assert.NotNull(provisioned.Client.TrialEndDate);
        Assert.True(provisioned.Client.TrialEndDate > DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task ProvisionClient_CreatesTheFirstLocation()
    {
        var (db, service, _) = NewService();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), PlatformSuperAdmin());

        Assert.Equal(1, provisioned.Client.LocationCount);
        var location = await db.Locations.SingleAsync(l => l.OrganizationId == provisioned.Client.Id);
        Assert.Equal("Main Clinic", location.Name);
        Assert.Equal("Springfield", location.City);
    }

    [Fact]
    public async Task CancelClient_SetsCancelledStatusAndFields()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), actor);

        var cancelled = await service.CancelClientAsync(provisioned.Client.ClientNumber!.Value, "Client stopped paying", actor);

        Assert.Equal(OrganizationStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAt);
    }

    [Fact]
    public async Task CancelClient_AlreadyCancelled_Throws()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), actor);
        await service.CancelClientAsync(provisioned.Client.ClientNumber!.Value, "reason", actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelClientAsync(provisioned.Client.ClientNumber!.Value, "reason again", actor));
    }

    [Fact]
    public async Task ActivateClient_FromCancelled_ClearsCancellationFields()
    {
        var (_, service, _) = NewService();
        var actor = PlatformSuperAdmin();
        var provisioned = await service.ProvisionClientAsync(SampleRequest(), actor);
        await service.CancelClientAsync(provisioned.Client.ClientNumber!.Value, "reason", actor);

        var reactivated = await service.ActivateClientAsync(provisioned.Client.ClientNumber!.Value, actor);

        Assert.Equal(OrganizationStatus.Active, reactivated.Status);
        Assert.Null(reactivated.CancelledAt);
    }

    [Fact]
    public async Task Provisioning_WritesClientAndAdminAuditEvents()
    {
        var (db, service, _) = NewService();
        var actor = PlatformSuperAdmin();

        await service.ProvisionClientAsync(SampleRequest(), actor);

        var events = await db.AuditEvents.Select(e => e.Action).ToListAsync();
        Assert.Contains("client.created", events);
        Assert.Contains("client_admin.created", events);
    }
}

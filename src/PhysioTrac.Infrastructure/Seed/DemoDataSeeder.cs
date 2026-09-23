using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>Idempotent local/demo dataset -- one clinic (Source Motion
/// Physical Therapy, this app's reference tenant), one login per staff
/// role with a known password, two patients, and the minimum billing
/// configuration (a payer, two service prices, two appointment types) so
/// every page in the app has something real to show on a fresh database.
///
/// Deliberately bypasses the real client-provisioning flow
/// (<c>IClientProvisioningService.ProvisionClientAsync</c>), which issues an
/// admin an email invitation with no usable password -- correct for real
/// onboarding, useless for a seed script whose whole point is a login that
/// works immediately. Never runs outside Development (see Program.cs); a
/// production database is never auto-seeded with fake credentials.</summary>
public static class DemoDataSeeder
{
    public const string DemoOrganizationSlug = "source-motion-pt";
    public const string DemoPassword = "DemoPass123!";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<PhysioTracDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await db.Organizations.AnyAsync(o => o.Slug == DemoOrganizationSlug, ct))
        {
            return;
        }

        var organization = new Organization
        {
            Name = "Source Motion Physical Therapy",
            Slug = DemoOrganizationSlug,
            SubscriptionTier = SubscriptionTier.Professional,
            Timezone = "America/New_York",
            SupportEmail = "support@sourcemotionpt.test",
            AddressLine1 = "100 Rehab Way",
            City = "Boston",
            State = "MA",
            ZipCode = "02110",
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        var admin = await CreateUserAsync(userManager, organization.Id, "admin", "admin@sourcemotionpt.test",
            "Alex", "Rivera", UserRole.Admin);
        var therapist = await CreateUserAsync(userManager, organization.Id, "therapist", "therapist@sourcemotionpt.test",
            "Jamie", "Chen", UserRole.Therapist, credential: "PT, DPT");
        await CreateUserAsync(userManager, organization.Id, "scheduler", "scheduler@sourcemotionpt.test",
            "Morgan", "Patel", UserRole.Scheduler);
        await CreateUserAsync(userManager, organization.Id, "biller", "biller@sourcemotionpt.test",
            "Casey", "Nguyen", UserRole.Biller);

        var provider = new Provider
        {
            OrganizationId = organization.Id,
            UserId = therapist.Id,
            FirstName = therapist.FirstName,
            LastName = therapist.LastName,
            Specialty = "Orthopedic Physical Therapy",
            Credentials = "PT, DPT",
        };
        db.Providers.Add(provider);

        db.Patients.AddRange(
            new Patient
            {
                OrganizationId = organization.Id,
                FirstName = "Taylor",
                LastName = "Brooks",
                DateOfBirth = new DateOnly(1987, 4, 12),
                Phone = "555-0101",
                Email = "taylor.brooks@example.test",
                Diagnoses = "Right knee ACL reconstruction, post-op",
                AssignedTherapistId = therapist.Id,
            },
            new Patient
            {
                OrganizationId = organization.Id,
                FirstName = "Riley",
                LastName = "Simmons",
                DateOfBirth = new DateOnly(1994, 11, 3),
                Phone = "555-0102",
                Email = "riley.simmons@example.test",
                Diagnoses = "Chronic low back pain",
                AssignedTherapistId = therapist.Id,
            });

        db.AppointmentTypes.AddRange(
            new AppointmentType
            {
                OrganizationId = organization.Id,
                Name = "Initial Evaluation",
                Description = "New patient evaluation and plan of care",
                DefaultDurationMinutes = 60,
                Price = 175.00m,
                RequiresNewPatient = true,
                DefaultKind = AppointmentKind.Evaluation,
            },
            new AppointmentType
            {
                OrganizationId = organization.Id,
                Name = "Follow-up Visit",
                Description = "Standard follow-up treatment session",
                DefaultDurationMinutes = 30,
                Price = 95.00m,
                DefaultKind = AppointmentKind.FollowUp,
            });

        db.ServicePrices.AddRange(
            new ServicePrice { OrganizationId = organization.Id, CptCode = "97110", Label = "Therapeutic Exercise", Price = 65.00m, CreatedById = admin.Id },
            new ServicePrice { OrganizationId = organization.Id, CptCode = "97140", Label = "Manual Therapy", Price = 55.00m, CreatedById = admin.Id });

        db.Payers.Add(new Payer
        {
            OrganizationId = organization.Id,
            Name = "Blue Cross Blue Shield",
            PayerId = "BCBS-DEMO",
            TimelyFilingDays = 90,
            CreatedById = admin.Id,
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager, Guid organizationId, string userName, string email,
        string firstName, string lastName, UserRole role, string? credential = null)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            OrganizationId = organizationId,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            Credential = credential,
            MustUseMfa = false,
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed demo user '{userName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }
}

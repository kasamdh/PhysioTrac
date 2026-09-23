using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>Idempotent local/demo dataset -- two independent tenants
/// (Source Motion Physical Therapy, org 1000, and Total Motion PT, org
/// 1001) with their own logins, patients, appointments, and clinical notes.
/// Two separate organizations exist specifically to prove tenant isolation:
/// every id below is generated independently per org, so an org-1000 user
/// who somehow ended up with an org-1001 record's id would hit
/// ITenantAccessService's cross-org rejection, not silently succeed.
///
/// Deliberately bypasses the real client-provisioning flow
/// (<c>IClientProvisioningService.ProvisionClientAsync</c>), which issues an
/// admin an email invitation with no usable password -- correct for real
/// onboarding, useless for a seed script whose whole point is a login that
/// works immediately. Never runs outside Development (see Program.cs); a
/// production database is never auto-seeded with fake credentials.</summary>
public static class DemoDataSeeder
{
    private const string SourceMotionSlug = "source-motion-pt";
    private const string TotalMotionSlug = "total-motion-pt";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<PhysioTracDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var seedOptions = services.GetRequiredService<IOptions<SeedOptions>>().Value;

        if (string.IsNullOrWhiteSpace(seedOptions.DemoPassword))
        {
            throw new InvalidOperationException(
                "Seed:DemoPassword is not set. Set the Seed__DemoPassword environment variable " +
                "(or a Development user secret) before starting the Api -- demo login credentials " +
                "are never hardcoded in source or checked-in appsettings.");
        }

        if (await db.Organizations.AnyAsync(o => o.Slug == SourceMotionSlug, ct))
        {
            return;
        }

        await SeedSourceMotionAsync(db, userManager, seedOptions.DemoPassword, ct);
        await SeedTotalMotionAsync(db, userManager, seedOptions.DemoPassword, ct);
    }

    /// <summary>Org 1000 -- the first real customer. Two physical locations
    /// (matching Source Motion's actual footprint) so multi-location
    /// scheduling/reporting has something real to show, not just one
    /// headquarters address standing in for everything.</summary>
    private static async Task SeedSourceMotionAsync(PhysioTracDbContext db, UserManager<ApplicationUser> userManager, string demoPassword, CancellationToken ct)
    {
        var organization = new Organization
        {
            ClientNumber = 1000,
            Name = "Source Motion Physical Therapy",
            Slug = SourceMotionSlug,
            SubscriptionTier = SubscriptionTier.Professional,
            Timezone = "America/New_York",
            SupportEmail = "support@sourcemotionpt.test",
            AddressLine1 = "100 Rehab Way",
            City = "Fuquay-Varina",
            State = "NC",
            ZipCode = "27526",
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        var fuquayVarina = new Location
        {
            OrganizationId = organization.Id,
            Name = "Fuquay-Varina",
            AddressLine1 = "100 Rehab Way",
            City = "Fuquay-Varina",
            State = "NC",
            ZipCode = "27526",
            Phone = "919-555-0110",
            Timezone = "America/New_York",
        };
        var raleigh = new Location
        {
            OrganizationId = organization.Id,
            Name = "Raleigh",
            AddressLine1 = "500 Wellness Blvd",
            City = "Raleigh",
            State = "NC",
            ZipCode = "27601",
            Phone = "919-555-0199",
            Timezone = "America/New_York",
        };
        db.Locations.AddRange(fuquayVarina, raleigh);

        var admin = await CreateUserAsync(userManager, organization.Id, "admin", "admin@sourcemotionpt.test",
            "Alex", "Rivera", UserRole.Admin, demoPassword, ct);
        var therapist = await CreateUserAsync(userManager, organization.Id, "therapist", "therapist@sourcemotionpt.test",
            "Jamie", "Chen", UserRole.Therapist, demoPassword, ct, credential: "PT, DPT");
        await CreateUserAsync(userManager, organization.Id, "scheduler", "scheduler@sourcemotionpt.test",
            "Morgan", "Patel", UserRole.Scheduler, demoPassword, ct);
        await CreateUserAsync(userManager, organization.Id, "biller", "biller@sourcemotionpt.test",
            "Casey", "Nguyen", UserRole.Biller, demoPassword, ct);

        var provider = new Provider
        {
            OrganizationId = organization.Id,
            UserId = therapist.Id,
            FirstName = therapist.FirstName,
            LastName = therapist.LastName,
            Specialty = "Orthopedic Physical Therapy",
            Credentials = "PT, DPT",
        };
        provider.Locations.Add(fuquayVarina);
        provider.Locations.Add(raleigh);
        db.Providers.Add(provider);
        await db.SaveChangesAsync(ct);

        db.ProviderLicenses.Add(new ProviderLicense
        {
            ProviderId = provider.Id,
            State = "NC",
            LicenseNumber = "NC-PT-88421",
            IssueDate = new DateOnly(2019, 6, 1),
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddYears(2)),
            Status = ProviderLicenseStatus.Active,
            IsCompactPrivilege = false,
        });

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

    /// <summary>Org 1001 -- a second, wholly independent tenant used only to
    /// prove isolation: its own admin/therapist logins, its own patient, its
    /// own appointment, and its own clinical note. None of these ids are
    /// derived from or shared with Source Motion's data in any way.</summary>
    private static async Task SeedTotalMotionAsync(PhysioTracDbContext db, UserManager<ApplicationUser> userManager, string demoPassword, CancellationToken ct)
    {
        var organization = new Organization
        {
            ClientNumber = 1001,
            Name = "Total Motion PT",
            Slug = TotalMotionSlug,
            SubscriptionTier = SubscriptionTier.Starter,
            Timezone = "America/Chicago",
            SupportEmail = "support@totalmotionpt.test",
            AddressLine1 = "42 Recovery Lane",
            City = "Austin",
            State = "TX",
            ZipCode = "78701",
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        var austin = new Location
        {
            OrganizationId = organization.Id,
            Name = "Austin",
            AddressLine1 = "42 Recovery Lane",
            City = "Austin",
            State = "TX",
            ZipCode = "78701",
            Phone = "512-555-0142",
            Timezone = "America/Chicago",
        };
        db.Locations.Add(austin);

        var admin = await CreateUserAsync(userManager, organization.Id, "tm.admin", "admin@totalmotionpt.test",
            "David", "Okafor", UserRole.Admin, demoPassword, ct);
        var therapist = await CreateUserAsync(userManager, organization.Id, "tm.therapist", "therapist@totalmotionpt.test",
            "Priya", "Sharma", UserRole.Therapist, demoPassword, ct, credential: "PT, DPT");

        var provider = new Provider
        {
            OrganizationId = organization.Id,
            UserId = therapist.Id,
            FirstName = therapist.FirstName,
            LastName = therapist.LastName,
            Specialty = "Sports Physical Therapy",
            Credentials = "PT, DPT",
        };
        provider.Locations.Add(austin);
        db.Providers.Add(provider);
        await db.SaveChangesAsync(ct);

        db.ProviderLicenses.Add(new ProviderLicense
        {
            ProviderId = provider.Id,
            State = "TX",
            LicenseNumber = "TX-PT-55210",
            IssueDate = new DateOnly(2021, 3, 15),
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddYears(2)),
            Status = ProviderLicenseStatus.Active,
            IsCompactPrivilege = false,
        });

        var patient = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Jordan",
            LastName = "Ellis",
            DateOfBirth = new DateOnly(1990, 8, 22),
            Phone = "512-555-0177",
            Email = "jordan.ellis@example.test",
            Diagnoses = "Rotator cuff tendinopathy",
            AssignedTherapistId = therapist.Id,
        };
        db.Patients.Add(patient);

        var appointmentType = new AppointmentType
        {
            OrganizationId = organization.Id,
            Name = "Initial Evaluation",
            Description = "New patient evaluation and plan of care",
            DefaultDurationMinutes = 60,
            Price = 165.00m,
            RequiresNewPatient = true,
            DefaultKind = AppointmentKind.Evaluation,
        };
        db.AppointmentTypes.Add(appointmentType);
        await db.SaveChangesAsync(ct);

        var appointmentStart = DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(9);
        db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id,
            TherapistId = therapist.Id,
            ProviderId = provider.Id,
            AppointmentTypeId = appointmentType.Id,
            Kind = AppointmentKind.Evaluation,
            Status = AppointmentStatus.Scheduled,
            StartsAt = appointmentStart,
            EndsAt = appointmentStart.AddMinutes(60),
            CreatedById = admin.Id,
        });

        db.ClinicalNotes.Add(new ClinicalNote
        {
            PatientId = patient.Id,
            TherapistId = therapist.Id,
            NoteType = NoteType.Evaluation,
            Status = NoteStatus.Draft,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
            DiagnosisSnapshot = patient.Diagnoses,
            Subjective = "Patient reports right shoulder pain with overhead activity, onset 6 weeks ago.",
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager, Guid organizationId, string userName, string email,
        string firstName, string lastName, UserRole role, string demoPassword, CancellationToken ct, string? credential = null)
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

        var result = await userManager.CreateAsync(user, demoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed demo user '{userName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }
}

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

        await SeedDiagnosisCodesAsync(db, ct);
        await SeedPlatformSuperAdminAsync(userManager, seedOptions.DemoPassword, ct);
        await SeedSourceMotionAsync(db, userManager, seedOptions.DemoPassword, ct);
        await SeedTotalMotionAsync(db, userManager, seedOptions.DemoPassword, ct);
    }

    /// <summary>Shared, org-independent ICD-10-CM reference data -- see
    /// DiagnosisCode's own doc comment. A small, PT-relevant slice, not a
    /// real full code set (tens of thousands of rows), enough for the demo
    /// patients below and for exercising PatientDiagnosesController/search.</summary>
    private static async Task SeedDiagnosisCodesAsync(PhysioTracDbContext db, CancellationToken ct)
    {
        db.DiagnosisCodes.AddRange(
            new DiagnosisCode { Code = "S83.511A", Description = "Sprain of anterior cruciate ligament of right knee, initial encounter" },
            new DiagnosisCode { Code = "S83.512A", Description = "Sprain of anterior cruciate ligament of left knee, initial encounter" },
            new DiagnosisCode { Code = "M54.50", Description = "Low back pain, unspecified" },
            new DiagnosisCode { Code = "M25.561", Description = "Pain in right knee" },
            new DiagnosisCode { Code = "M25.562", Description = "Pain in left knee" },
            new DiagnosisCode { Code = "M75.100", Description = "Unspecified rotator cuff tear or rupture of right shoulder, not specified as traumatic" },
            new DiagnosisCode { Code = "M75.101", Description = "Unspecified rotator cuff tear or rupture of left shoulder, not specified as traumatic" },
            new DiagnosisCode { Code = "M17.11", Description = "Unilateral primary osteoarthritis, right knee" },
            new DiagnosisCode { Code = "M17.12", Description = "Unilateral primary osteoarthritis, left knee" },
            new DiagnosisCode { Code = "M62.830", Description = "Muscle spasm of back" },
            new DiagnosisCode { Code = "M54.2", Description = "Cervicalgia" },
            new DiagnosisCode { Code = "S93.401A", Description = "Sprain of unspecified ligament of right ankle, initial encounter" },
            new DiagnosisCode { Code = "S93.402A", Description = "Sprain of unspecified ligament of left ankle, initial encounter" },
            new DiagnosisCode { Code = "G56.00", Description = "Carpal tunnel syndrome, unspecified upper limb" },
            new DiagnosisCode { Code = "M79.1", Description = "Myalgia" },
            new DiagnosisCode { Code = "R26.2", Description = "Difficulty in walking, not elsewhere classified" },
            new DiagnosisCode { Code = "E11.9", Description = "Type 2 diabetes mellitus without complications" },
            new DiagnosisCode { Code = "I10", Description = "Essential (primary) hypertension" });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The one account with no standing organization at all --
    /// IsPlatformSuperAdmin requires Role == SuperAdmin AND OrganizationId
    /// == null, so unlike every other role this is seeded once, globally,
    /// not per-org.</summary>
    private static async Task SeedPlatformSuperAdminAsync(UserManager<ApplicationUser> userManager, string demoPassword, CancellationToken ct)
    {
        await CreateUserAsync(userManager, organizationId: null, "superadmin", "superadmin@physiotrac.test",
            "Platform", "Admin", UserRole.SuperAdmin, demoPassword, ct);
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
        await CreateUserAsync(userManager, organization.Id, "director", "director@sourcemotionpt.test",
            "Devon", "Reyes", UserRole.Director, demoPassword, ct, credential: "PT, DPT");
        var therapist = await CreateUserAsync(userManager, organization.Id, "therapist", "therapist@sourcemotionpt.test",
            "Jamie", "Chen", UserRole.Therapist, demoPassword, ct, credential: "PT, DPT");
        await CreateUserAsync(userManager, organization.Id, "assistant", "assistant@sourcemotionpt.test",
            "Avery", "Kim", UserRole.Assistant, demoPassword, ct, credential: "PTA");
        await CreateUserAsync(userManager, organization.Id, "scheduler", "scheduler@sourcemotionpt.test",
            "Morgan", "Patel", UserRole.Scheduler, demoPassword, ct);
        await CreateUserAsync(userManager, organization.Id, "biller", "biller@sourcemotionpt.test",
            "Casey", "Nguyen", UserRole.Biller, demoPassword, ct);
        await CreateUserAsync(userManager, organization.Id, "compliance", "compliance@sourcemotionpt.test",
            "Corey", "Diaz", UserRole.Compliance, demoPassword, ct);

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

        var pcp = new ReferringProvider
        {
            OrganizationId = organization.Id,
            FirstName = "Nadia",
            LastName = "Farouk",
            Specialty = "Family Medicine",
            Phone = "919-555-0188",
            Npi = "1122330099",
        };
        db.ReferringProviders.Add(pcp);
        await db.SaveChangesAsync(ct);

        // Taylor's portal login -- the only seeded Patient-role account for
        // this org, so the portal path (PatientsFor's PortalUserId match) has
        // a real, working login to test against instead of only ever being
        // exercised by staff-role logins.
        var taylorPortalUser = await CreateUserAsync(userManager, organization.Id, "patient", "taylor.brooks@example.test",
            "Taylor", "Brooks", UserRole.Patient, demoPassword, ct);

        var taylor = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Taylor",
            LastName = "Brooks",
            DateOfBirth = new DateOnly(1987, 4, 12),
            Phone = "555-0101",
            Email = "taylor.brooks@example.test",
            Address = "212 Maple St, Fuquay-Varina, NC 27526",
            EmergencyContact = "Jordan Brooks (spouse) - 555-0191",
            PreferredLanguage = "English",
            Diagnoses = "Right knee ACL reconstruction, post-op",
            AssignedTherapistId = therapist.Id,
            PrimaryLocationId = fuquayVarina.Id,
            PrimaryCareProviderId = pcp.Id,
            ReferringProviderId = pcp.Id,
            PortalUserId = taylorPortalUser.Id,
        };
        var riley = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Riley",
            LastName = "Simmons",
            DateOfBirth = new DateOnly(1994, 11, 3),
            Phone = "555-0102",
            Email = "riley.simmons@example.test",
            Address = "88 Birchwood Ln, Raleigh, NC 27601",
            EmergencyContact = "Casey Simmons (sibling) - 555-0192",
            PreferredLanguage = "English",
            Diagnoses = "Chronic low back pain",
            AssignedTherapistId = therapist.Id,
            PrimaryLocationId = raleigh.Id,
        };
        var harper = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Harper",
            LastName = "Ellison",
            DateOfBirth = new DateOnly(1978, 2, 19),
            Phone = "555-0103",
            Email = "harper.ellison@example.test",
            Address = "14 Sunset Ridge Dr, Fuquay-Varina, NC 27526",
            EmergencyContact = "Morgan Ellison (spouse) - 555-0193",
            PreferredLanguage = "English",
            Diagnoses = "Right shoulder rotator cuff tendinopathy; type 2 diabetes",
            AssignedTherapistId = therapist.Id,
            PrimaryLocationId = fuquayVarina.Id,
            PrimaryCareProviderId = pcp.Id,
        };
        var quinn = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Quinn",
            LastName = "Alvarez",
            DateOfBirth = new DateOnly(2001, 7, 30),
            Phone = "555-0104",
            Email = "quinn.alvarez@example.test",
            Address = "305 Lakeview Ct, Raleigh, NC 27601",
            EmergencyContact = "Pat Alvarez (parent) - 555-0194",
            PreferredLanguage = "Spanish",
            Diagnoses = "Right ankle sprain",
            AssignedTherapistId = therapist.Id,
            PrimaryLocationId = raleigh.Id,
            Status = PatientStatus.Discharged,
        };
        db.Patients.AddRange(taylor, riley, harper, quinn);
        await db.SaveChangesAsync(ct);

        db.PatientAllergies.AddRange(
            new PatientAllergy { PatientId = taylor.Id, Allergen = "Penicillin", Reaction = "Hives", Severity = AllergySeverity.Moderate, RecordedById = admin.Id },
            new PatientAllergy { PatientId = harper.Id, Allergen = "Latex", Reaction = "Contact dermatitis", Severity = AllergySeverity.Mild, RecordedById = admin.Id },
            new PatientAllergy { PatientId = harper.Id, Allergen = "Sulfa drugs", Reaction = "Rash", Severity = AllergySeverity.Moderate, RecordedById = admin.Id });

        db.PatientMedications.AddRange(
            new PatientMedication { PatientId = taylor.Id, Name = "Ibuprofen", Dosage = "400mg", Frequency = "As needed", StartDate = new DateOnly(2026, 6, 1), RecordedById = admin.Id },
            new PatientMedication { PatientId = harper.Id, Name = "Metformin", Dosage = "500mg", Frequency = "Twice daily", PrescribingProvider = "Dr. Nadia Farouk", StartDate = new DateOnly(2023, 1, 15), RecordedById = admin.Id },
            new PatientMedication { PatientId = harper.Id, Name = "Lisinopril", Dosage = "10mg", Frequency = "Once daily", PrescribingProvider = "Dr. Nadia Farouk", StartDate = new DateOnly(2022, 9, 10), RecordedById = admin.Id });

        var aclCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "S83.511A", ct);
        var lowBackCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "M54.50", ct);
        var rotatorCuffCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "M75.100", ct);
        var diabetesCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "E11.9", ct);
        var ankleSprainCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "S93.401A", ct);

        db.PatientDiagnoses.AddRange(
            new PatientDiagnosis { PatientId = taylor.Id, DiagnosisCodeId = aclCode.Id, IsPrimary = true, DiagnosedDate = new DateOnly(2026, 5, 20) },
            new PatientDiagnosis { PatientId = riley.Id, DiagnosisCodeId = lowBackCode.Id, IsPrimary = true, DiagnosedDate = new DateOnly(2026, 3, 4) },
            new PatientDiagnosis { PatientId = harper.Id, DiagnosisCodeId = rotatorCuffCode.Id, IsPrimary = true, DiagnosedDate = new DateOnly(2026, 4, 2) },
            new PatientDiagnosis { PatientId = harper.Id, DiagnosisCodeId = diabetesCode.Id, IsPrimary = false, DiagnosedDate = new DateOnly(2023, 1, 15) },
            new PatientDiagnosis { PatientId = quinn.Id, DiagnosisCodeId = ankleSprainCode.Id, IsPrimary = true, DiagnosedDate = new DateOnly(2026, 8, 11), ResolvedDate = new DateOnly(2026, 9, 15) });

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

        var bcbs = new Payer
        {
            OrganizationId = organization.Id,
            Name = "Blue Cross Blue Shield",
            PayerId = "BCBS-DEMO",
            TimelyFilingDays = 90,
            CreatedById = admin.Id,
        };
        var aetna = new Payer
        {
            OrganizationId = organization.Id,
            Name = "Aetna",
            PayerId = "AETNA-DEMO",
            TimelyFilingDays = 90,
            CreatedById = admin.Id,
        };
        db.Payers.AddRange(bcbs, aetna);
        await db.SaveChangesAsync(ct);

        db.PatientInsurancePolicies.AddRange(
            new PatientInsurance
            {
                OrganizationId = organization.Id, PatientId = taylor.Id, PayerId = bcbs.Id, Rank = InsuranceRank.Primary,
                PlanName = "BCBS PPO", MemberId = "BCBS-TB-4471", SubscriberName = "Taylor Brooks",
                SubscriberDateOfBirth = taylor.DateOfBirth, RelationshipToSubscriber = RelationshipToSubscriber.Self,
                EffectiveDate = new DateOnly(2026, 1, 1), Copay = 40m, CreatedById = admin.Id,
            },
            // Harper carries two policies -- primary through their own
            // employer, secondary as a dependent on a spouse's plan -- the
            // exact multi-policy, subscriber-details scenario Rank/
            // RelationshipToSubscriber exist to model.
            new PatientInsurance
            {
                OrganizationId = organization.Id, PatientId = harper.Id, PayerId = aetna.Id, Rank = InsuranceRank.Primary,
                PlanName = "Aetna Choice POS II", MemberId = "AETNA-HE-8820", SubscriberName = "Harper Ellison",
                SubscriberDateOfBirth = harper.DateOfBirth, RelationshipToSubscriber = RelationshipToSubscriber.Self,
                EffectiveDate = new DateOnly(2025, 1, 1), Copay = 30m, CreatedById = admin.Id,
            },
            new PatientInsurance
            {
                OrganizationId = organization.Id, PatientId = harper.Id, PayerId = bcbs.Id, Rank = InsuranceRank.Secondary,
                PlanName = "BCBS PPO", MemberId = "BCBS-ME-1290", SubscriberName = "Morgan Ellison",
                SubscriberDateOfBirth = new DateOnly(1976, 5, 8), RelationshipToSubscriber = RelationshipToSubscriber.Spouse,
                EffectiveDate = new DateOnly(2024, 1, 1), CreatedById = admin.Id,
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
        await CreateUserAsync(userManager, organization.Id, "tm.director", "director@totalmotionpt.test",
            "Dana", "Fitzgerald", UserRole.Director, demoPassword, ct, credential: "PT, DPT");
        var therapist = await CreateUserAsync(userManager, organization.Id, "tm.therapist", "therapist@totalmotionpt.test",
            "Priya", "Sharma", UserRole.Therapist, demoPassword, ct, credential: "PT, DPT");
        await CreateUserAsync(userManager, organization.Id, "tm.assistant", "assistant@totalmotionpt.test",
            "Ashley", "Nguyen", UserRole.Assistant, demoPassword, ct, credential: "PTA");
        await CreateUserAsync(userManager, organization.Id, "tm.scheduler", "scheduler@totalmotionpt.test",
            "Sam", "Torres", UserRole.Scheduler, demoPassword, ct);
        await CreateUserAsync(userManager, organization.Id, "tm.biller", "biller@totalmotionpt.test",
            "Bailey", "Wong", UserRole.Biller, demoPassword, ct);
        await CreateUserAsync(userManager, organization.Id, "tm.compliance", "compliance@totalmotionpt.test",
            "Charlie", "Osei", UserRole.Compliance, demoPassword, ct);

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

        var tmPcp = new ReferringProvider
        {
            OrganizationId = organization.Id,
            FirstName = "Wesley",
            LastName = "Cho",
            Specialty = "Internal Medicine",
            Phone = "512-555-0166",
            Npi = "1199887766",
        };
        db.ReferringProviders.Add(tmPcp);
        await db.SaveChangesAsync(ct);

        var jordanPortalUser = await CreateUserAsync(userManager, organization.Id, "tm.patient", "jordan.ellis@example.test",
            "Jordan", "Ellis", UserRole.Patient, demoPassword, ct);

        var patient = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Jordan",
            LastName = "Ellis",
            DateOfBirth = new DateOnly(1990, 8, 22),
            Phone = "512-555-0177",
            Email = "jordan.ellis@example.test",
            Address = "77 Congress Ave, Austin, TX 78701",
            EmergencyContact = "Sam Ellis (parent) - 512-555-0188",
            PreferredLanguage = "English",
            Diagnoses = "Rotator cuff tendinopathy",
            AssignedTherapistId = therapist.Id,
            PrimaryLocationId = austin.Id,
            PrimaryCareProviderId = tmPcp.Id,
            ReferringProviderId = tmPcp.Id,
            PortalUserId = jordanPortalUser.Id,
        };
        var reese = new Patient
        {
            OrganizationId = organization.Id,
            FirstName = "Reese",
            LastName = "Whitfield",
            DateOfBirth = new DateOnly(1982, 12, 5),
            Phone = "512-555-0199",
            Email = "reese.whitfield@example.test",
            Address = "410 South Lamar Blvd, Austin, TX 78704",
            EmergencyContact = "Drew Whitfield (spouse) - 512-555-0200",
            PreferredLanguage = "English",
            Diagnoses = "Lumbar strain",
            AssignedTherapistId = therapist.Id,
            PrimaryLocationId = austin.Id,
            PrimaryCareProviderId = tmPcp.Id,
        };
        db.Patients.AddRange(patient, reese);
        await db.SaveChangesAsync(ct);

        db.PatientAllergies.Add(
            new PatientAllergy { PatientId = patient.Id, Allergen = "Codeine", Reaction = "Nausea", Severity = AllergySeverity.Mild, RecordedById = admin.Id });

        db.PatientMedications.Add(
            new PatientMedication { PatientId = reese.Id, Name = "Naproxen", Dosage = "220mg", Frequency = "Twice daily as needed", StartDate = new DateOnly(2026, 7, 1), RecordedById = admin.Id });

        var tmRotatorCuffCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "M75.100", ct);
        var tmLowBackCode = await db.DiagnosisCodes.FirstAsync(c => c.Code == "M54.50", ct);
        db.PatientDiagnoses.AddRange(
            new PatientDiagnosis { PatientId = patient.Id, DiagnosisCodeId = tmRotatorCuffCode.Id, IsPrimary = true, DiagnosedDate = new DateOnly(2026, 8, 1) },
            new PatientDiagnosis { PatientId = reese.Id, DiagnosisCodeId = tmLowBackCode.Id, IsPrimary = true, DiagnosedDate = new DateOnly(2026, 7, 1) });

        var tmPayer = new Payer
        {
            OrganizationId = organization.Id,
            Name = "United Healthcare",
            PayerId = "UHC-DEMO",
            TimelyFilingDays = 90,
            CreatedById = admin.Id,
        };
        db.Payers.Add(tmPayer);
        await db.SaveChangesAsync(ct);

        db.PatientInsurancePolicies.Add(new PatientInsurance
        {
            OrganizationId = organization.Id, PatientId = patient.Id, PayerId = tmPayer.Id, Rank = InsuranceRank.Primary,
            PlanName = "UHC Choice Plus", MemberId = "UHC-JE-3391", SubscriberName = "Jordan Ellis",
            SubscriberDateOfBirth = patient.DateOfBirth, RelationshipToSubscriber = RelationshipToSubscriber.Self,
            EffectiveDate = new DateOnly(2026, 1, 1), Copay = 35m, CreatedById = admin.Id,
        });

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
        UserManager<ApplicationUser> userManager, Guid? organizationId, string userName, string email,
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

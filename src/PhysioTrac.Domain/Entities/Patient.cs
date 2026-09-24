using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Minimum-necessary demographics and PT chart header.</summary>
public class Patient : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string MedicalRecordNumber { get; set; } = "SM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? Diagnoses { get; set; }
    public string? Precautions { get; set; }
    public string? PreferredLanguage { get; set; }

    /// <summary>The patient's own home clinic for scheduling/reporting
    /// defaults -- distinct from a Provider's Locations (which locations a
    /// clinician works out of). Nullable: a patient isn't required to have
    /// one, e.g. a fully-remote telehealth caseload.</summary>
    public Guid? PrimaryLocationId { get; set; }
    public Location? PrimaryLocation { get; set; }

    /// <summary>The patient's primary care physician -- an outside provider,
    /// same category as ReferringProviderId below, and often (but not
    /// always) the same actual person. Deliberately a separate field rather
    /// than reusing ReferringProviderId for both roles.</summary>
    public Guid? PrimaryCareProviderId { get; set; }
    public ReferringProvider? PrimaryCareProvider { get; set; }

    public string? PharmacyName { get; set; }
    public string? PharmacyPhone { get; set; }
    public string? PharmacyAddress { get; set; }
    public ContactMethod PreferredContactMethod { get; set; } = ContactMethod.Email;

    /// <summary>Whether this patient receives the (content-free) "you have a
    /// new secure message" email.</summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>Opt-in for Mobile Care text updates. Off by default, unlike
    /// email — SMS consent is opt-in, not opt-out.</summary>
    public bool SmsNotificationsEnabled { get; set; } = false;

    public Guid? AssignedTherapistId { get; set; }

    /// <summary>The outside physician who referred this patient in, if any --
    /// distinct from PrimaryCareProviderId above; often, but not always,
    /// the same actual person.</summary>
    public Guid? ReferringProviderId { get; set; }
    public ReferringProvider? ReferringProvider { get; set; }

    /// <summary>The login identity (role=Patient) this chart's portal account
    /// uses, if one has been issued.</summary>
    public Guid? PortalUserId { get; set; }

    public PatientStatus Status { get; set; } = PatientStatus.Active;

    /// <summary>Soft delete -- a chart is never hard-deleted (billing/audit
    /// history must survive), matching PatientDocument's own DeletedAt
    /// convention. TenantAccessService.PatientsFor excludes these by
    /// default; RequirePatientAccessAsync still 403s rather than exposing
    /// whether a given id ever existed.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
    public bool IsDeleted => DeletedAt is not null;

    public string FullName => (FirstName + " " + LastName).Trim();

    public int Age
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - DateOfBirth.Year;
            var birthdayHasPassedThisYear = today.Month > DateOfBirth.Month
                || (today.Month == DateOfBirth.Month && today.Day >= DateOfBirth.Day);
            if (!birthdayHasPassedThisYear) age--;
            return age;
        }
    }

    public ICollection<ClinicalNote> Notes { get; set; } = new List<ClinicalNote>();
    public ICollection<FunctionalGoal> Goals { get; set; } = new List<FunctionalGoal>();
    public ICollection<OutcomeScore> Outcomes { get; set; } = new List<OutcomeScore>();
    public ICollection<PatientDocument> Documents { get; set; } = new List<PatientDocument>();
    public ICollection<Consent> Consents { get; set; } = new List<Consent>();
    public ICollection<HomeExerciseProgram> HomeExercisePrograms { get; set; } = new List<HomeExerciseProgram>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<PatientAllergy> Allergies { get; set; } = new List<PatientAllergy>();
    public ICollection<PatientMedication> Medications { get; set; } = new List<PatientMedication>();
    public ICollection<PatientDiagnosis> DiagnosisRecords { get; set; } = new List<PatientDiagnosis>();
}

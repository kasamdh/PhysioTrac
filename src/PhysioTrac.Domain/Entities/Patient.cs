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

    /// <summary>The login identity (role=Patient) this chart's portal account
    /// uses, if one has been issued.</summary>
    public Guid? PortalUserId { get; set; }

    public PatientStatus Status { get; set; } = PatientStatus.Active;

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
}

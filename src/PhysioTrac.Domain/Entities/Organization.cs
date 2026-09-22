using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Tenant boundary for all patient data.</summary>
public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public long? ClientNumber { get; set; }
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Starter;
    public string Timezone { get; set; } = "America/New_York";
    public string? PortalUrl { get; set; }
    public string? LogoPath { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? NpiNumber { get; set; }
    public string? TaxId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string Country { get; set; } = "United States";
    public string? Comments { get; set; }

    /// <summary>Whether a PTA/assistant-authored note requires a supervising
    /// PT/Director cosignature before it's final. Conservative default (true).</summary>
    public bool PtaCosignRequired { get; set; } = true;

    public DateTimeOffset? OnboardingCompletedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? SuspendedAt { get; set; }
    public Guid? SuspendedById { get; set; }
    public string? SuspensionReason { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedById { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public ICollection<Location> Locations { get; set; } = new List<Location>();
    public ICollection<Patient> Patients { get; set; } = new List<Patient>();

    public string Initials
    {
        get
        {
            var words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "CW";
            return string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
        }
    }
}

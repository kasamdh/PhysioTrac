using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>An outside physician/provider who referred a patient in --
/// distinct from <see cref="Provider"/> (this clinic's own clinicians).
/// Referral-source reporting (module 8 in the spec) groups patients by
/// this record.</summary>
public class ReferringProvider : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Npi { get; set; }
    public string? Specialty { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedById { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

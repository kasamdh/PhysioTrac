using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Operational clinic location or treatment site for a tenant.</summary>
public class Location : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string Timezone { get; set; } = "America/Los_Angeles";
    public bool IsActive { get; set; } = true;

    public ICollection<Provider> Providers { get; set; } = new List<Provider>();
}

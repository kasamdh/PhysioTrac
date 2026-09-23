using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Clinical provider profile tied to a tenant and optionally a user account.</summary>
public class Provider : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>Optional link to the login identity this provider is. Stored
    /// by id only — Domain doesn't reference Infrastructure's ApplicationUser.</summary>
    public Guid? UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? Credentials { get; set; }
    public string? NpiNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public bool OnlineBookingEnabled { get; set; } = true;

    /// <summary>Explicit opt-in for live GPS capture during a home-visit
    /// travel segment. Off by default — separate from account consent.</summary>
    public bool LocationSharingEnabled { get; set; }

    public string? Bio { get; set; }

    public ICollection<Location> Locations { get; set; } = new List<Location>();
    public ICollection<ProviderAppointmentType> AppointmentTypeLinks { get; set; } = new List<ProviderAppointmentType>();
    public ICollection<ProviderAvailability> Availabilities { get; set; } = new List<ProviderAvailability>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<ProviderLicense> Licenses { get; set; } = new List<ProviderLicense>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

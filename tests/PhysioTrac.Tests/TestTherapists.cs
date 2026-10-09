using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Tests;

/// <summary>AppointmentService only accepts a TherapistId that's a real,
/// active Therapist/Assistant login in the caller's own organization, so
/// tests booking appointments need an actual user row rather than a bare
/// Guid.NewGuid().</summary>
public static class TestTherapists
{
    public static Guid Add(PhysioTracDbContext db, Guid organizationId, UserRole role = UserRole.Therapist)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"therapist-{id:N}",
            OrganizationId = organizationId,
            FirstName = "Test",
            LastName = "Therapist",
            Role = role,
        });
        db.SaveChanges();
        return id;
    }

    /// <summary>A provider can only be booked with an active, unexpired
    /// license (for the location's state when there is one).</summary>
    public static ProviderLicense ValidLicense(string state = "NC") => new()
    {
        State = state,
        LicenseNumber = $"{state}-PT-{Random.Shared.Next(10000, 99999)}",
        ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
        Status = ProviderLicenseStatus.Active,
    };
}

using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Tests;

public class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public UserRole Role { get; set; }
    public bool IsPlatformSuperAdmin { get; set; }
}

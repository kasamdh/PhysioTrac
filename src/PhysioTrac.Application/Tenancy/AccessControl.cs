using System.Collections;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Tenancy;

/// <summary>Global switch for role-based access control.
///
/// While the product is still being built, the team has chosen to run with
/// role checks OFF so every staff login can use every module ("everyone is
/// a super admin"); proper roles and permissions come back once the
/// functionality is complete. Rather than deleting the existing checks, they
/// all consult this switch, so turning access control back on restores the
/// current behavior exactly.
///
/// Set once at startup from configuration (<c>AccessControl:Enabled</c>).
/// Defaults to ON -- tests and any environment that doesn't opt out keep
/// full enforcement -- and the hosts refuse to start with it OFF outside
/// Development.
///
/// What stays enforced even when OFF, because it's data isolation rather
/// than role permissions: authentication, organization (tenant) isolation,
/// and Patient-role portal accounts being limited to their own chart. The
/// platform SuperAdmin -- who belongs to no organization -- works inside
/// <see cref="SuperAdminOrganizationId"/> for the clinic modules.</summary>
public static class AccessControl
{
    private static volatile bool _enabled = true;

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>True when a role check should be waived for this caller:
    /// access control is off and they're anyone but a Patient-role portal
    /// account -- staff and the platform SuperAdmin alike.</summary>
    public static bool Bypasses(UserRole role) => !_enabled && role != UserRole.Patient;

    /// <summary>While access control is off, the organization a platform
    /// SuperAdmin (who belongs to none) works inside for every clinic
    /// module -- resolved at startup to the first active organization.
    /// Unused when access control is on.</summary>
    public static Guid? SuperAdminOrganizationId { get; set; }
}

/// <summary>A named group of roles (see <see cref="RoleSets"/>) whose
/// <see cref="Contains"/> also answers yes for any staff role while
/// <see cref="AccessControl"/> is off -- so every existing
/// <c>RoleSets.X.Contains(role)</c> check and policy opens up without
/// touching its call site. Enumeration still yields only the real members.</summary>
public sealed class RoleSet : IReadOnlySet<UserRole>
{
    private readonly HashSet<UserRole> _roles;

    public RoleSet(IEnumerable<UserRole> roles) => _roles = new HashSet<UserRole>(roles);

    public bool Contains(UserRole item) => AccessControl.Bypasses(item) || _roles.Contains(item);

    public int Count => _roles.Count;
    public IEnumerator<UserRole> GetEnumerator() => _roles.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool IsProperSubsetOf(IEnumerable<UserRole> other) => _roles.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<UserRole> other) => _roles.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<UserRole> other) => _roles.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<UserRole> other) => _roles.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<UserRole> other) => _roles.Overlaps(other);
    public bool SetEquals(IEnumerable<UserRole> other) => _roles.SetEquals(other);
}

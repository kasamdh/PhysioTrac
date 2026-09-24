using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Tenancy;

/// <summary>Named role groups shared across services/controllers — mirrors
/// the constants at the top of the original `care/access.py`.</summary>
public static class RoleSets
{
    public static readonly IReadOnlySet<UserRole> Clinical = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Compliance
    };

    public static readonly IReadOnlySet<UserRole> Scheduling = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Scheduler
    };

    public static readonly IReadOnlySet<UserRole> Billing = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Biller, UserRole.Director
    };

    /// <summary>Front desk collects payments (e.g. a copay at check-in)
    /// without the full billing visibility <see cref="Billing"/> grants.</summary>
    public static readonly IReadOnlySet<UserRole> PaymentCollection = new HashSet<UserRole>(Billing)
    {
        UserRole.Scheduler
    };

    /// <summary>Uploading/managing a patient's documents spans front-desk
    /// intake scans, clinical uploads, and billing (insurance card/EOB
    /// scans) -- effectively every staff role except Patient.</summary>
    public static readonly IReadOnlySet<UserRole> DocumentManagement = new HashSet<UserRole>(Clinical)
    {
        UserRole.Scheduler, UserRole.Biller
    };

    /// <summary>Organization-level administration: clinic locations, the
    /// organization profile itself, and staff accounts/roles. Deliberately
    /// narrower than <see cref="Clinical"/> -- a Therapist or Compliance
    /// officer has clinical access but no business managing which locations
    /// exist or who else has a login.</summary>
    public static readonly IReadOnlySet<UserRole> OrganizationAdministration = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director
    };
}

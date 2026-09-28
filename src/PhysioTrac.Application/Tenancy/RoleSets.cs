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

    /// <summary>May knowingly book outside a provider's availability, or
    /// double-book when the organization allows it, with a recorded reason
    /// (audited as schedule.conflict_override).</summary>
    public static readonly IReadOnlySet<UserRole> ScheduleOverride = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director
    };

    /// <summary>May change providers' weekly working hours and time off --
    /// the front desk runs the schedule, so Scheduler alongside admins.
    /// Clinicians see their hours but don't edit them.</summary>
    public static readonly IReadOnlySet<UserRole> AvailabilityManagement = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director, UserRole.Scheduler
    };

    /// <summary>Every staff role a tenant can have -- i.e. everyone except
    /// the platform-level SuperAdmin (who has no standing org access at
    /// all) and Patient (the portal role). Used by dashboards whose content
    /// isn't sensitive enough to need a narrower gate but still shouldn't be
    /// patient-portal-reachable.</summary>
    public static readonly IReadOnlySet<UserRole> AllStaff = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant,
        UserRole.Scheduler, UserRole.Biller, UserRole.Compliance,
    };
}

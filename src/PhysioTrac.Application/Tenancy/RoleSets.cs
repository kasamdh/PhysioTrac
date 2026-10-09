using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Tenancy;

/// <summary>Named role groups shared across services/controllers — mirrors
/// the constants at the top of the original `care/access.py`.</summary>
public static class RoleSets
{
    public static readonly IReadOnlySet<UserRole> Clinical = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Compliance
    ]);

    public static readonly IReadOnlySet<UserRole> Scheduling = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Scheduler
    ]);

    public static readonly IReadOnlySet<UserRole> Billing = new RoleSet(
    [
        UserRole.Admin, UserRole.Biller, UserRole.Director
    ]);

    /// <summary>Front desk collects payments (e.g. a copay at check-in)
    /// without the full billing visibility <see cref="Billing"/> grants.</summary>
    public static readonly IReadOnlySet<UserRole> PaymentCollection = new RoleSet([.. Billing,
        UserRole.Scheduler
    ]);

    /// <summary>Uploading/managing a patient's documents spans front-desk
    /// intake scans, clinical uploads, and billing (insurance card/EOB
    /// scans) -- effectively every staff role except Patient.</summary>
    public static readonly IReadOnlySet<UserRole> DocumentManagement = new RoleSet([.. Clinical,
        UserRole.Scheduler, UserRole.Biller
    ]);

    /// <summary>Organization-level administration: clinic locations, the
    /// organization profile itself, and staff accounts/roles. Deliberately
    /// narrower than <see cref="Clinical"/> -- a Therapist or Compliance
    /// officer has clinical access but no business managing which locations
    /// exist or who else has a login.</summary>
    public static readonly IReadOnlySet<UserRole> OrganizationAdministration = new RoleSet(
    [
        UserRole.Admin, UserRole.Director
    ]);

    /// <summary>Adds and edits the clinic's own exercises and exercise
    /// images (hep.manage_exercise_library / hep.manage_exercise_images).
    /// Platform exercises are changed only by the platform super admin.</summary>
    public static readonly IReadOnlySet<UserRole> ExerciseLibrary = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Therapist
    ]);

    /// <summary>May knowingly book outside a provider's availability, or
    /// double-book when the organization allows it, with a recorded reason
    /// (audited as schedule.conflict_override). PTs and PTAs have the same
    /// schedule authority as admins (clinic decision, 2026-09-28).</summary>
    public static readonly IReadOnlySet<UserRole> ScheduleOverride = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant
    ]);

    /// <summary>May change providers' weekly working hours and time off:
    /// admins, the front desk, and -- with the same schedule authority as
    /// admins -- PTs and PTAs.</summary>
    public static readonly IReadOnlySet<UserRole> AvailabilityManagement = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Scheduler, UserRole.Therapist, UserRole.Assistant
    ]);

    /// <summary>May read the organization's activity log (Administration ›
    /// Logs): administrators, plus the compliance officer whose job it is.</summary>
    public static readonly IReadOnlySet<UserRole> AuditLogReview = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Compliance
    ]);

    /// <summary>Every staff role a tenant can have -- i.e. everyone except
    /// the platform-level SuperAdmin (who has no standing org access at
    /// all) and Patient (the portal role). Used by dashboards whose content
    /// isn't sensitive enough to need a narrower gate but still shouldn't be
    /// patient-portal-reachable.</summary>
    public static readonly IReadOnlySet<UserRole> AllStaff = new RoleSet(
    [
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant,
        UserRole.Scheduler, UserRole.Biller, UserRole.Compliance
    ]);
}

using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>An approved intervention clinicians pick from in the flowsheet,
/// with its CPT code, timed/untimed billing nature and default dosage.</summary>
public class InterventionLibraryItem : BaseEntity, IUserStamped
{
    /// <summary>Stable code for built-in items (e.g. "bridges").</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InterventionCategory Category { get; set; }
    public string? CptCode { get; set; }
    public bool IsTimed { get; set; } = true;
    public string? BodyRegion { get; set; }
    public string? Description { get; set; }
    public int? DefaultSets { get; set; }
    public int? DefaultRepetitions { get; set; }
    public string? DefaultResistance { get; set; }
    public string? DefaultDuration { get; set; }
    public string? DefaultEquipment { get; set; }
    public string? DefaultPosition { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>A reusable set of interventions added to a flowsheet in one step
/// (e.g. "Knee strength progression"). Owned by a clinician, or shared with
/// the clinic when OwnerUserId is null.</summary>
public class InterventionGroup : BaseEntity, IUserStamped
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public ICollection<InterventionGroupItem> Items { get; set; } = new List<InterventionGroupItem>();
}

public class InterventionGroupItem : BaseEntity
{
    public Guid GroupId { get; set; }
    public InterventionGroup? Group { get; set; }

    public Guid? LibraryItemId { get; set; }
    public InterventionLibraryItem? LibraryItem { get; set; }

    public string Name { get; set; } = string.Empty;
    public InterventionCategory Category { get; set; }
    public string? CptCode { get; set; }
    public bool IsTimed { get; set; } = true;
    public int? Sets { get; set; }
    public int? Repetitions { get; set; }
    public string? Resistance { get; set; }
    public string? Duration { get; set; }
    public string? Equipment { get; set; }
    public string? Position { get; set; }
    public int Order { get; set; }
}

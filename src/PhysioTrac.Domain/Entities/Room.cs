using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>A physical treatment room/bay within a location -- the resource
/// "room conflict" detection actually checks against. Distinct from Location
/// itself (a whole clinic site, many rooms) and from Provider (a person, who
/// can move between rooms).</summary>
public class Room : BaseEntity
{
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

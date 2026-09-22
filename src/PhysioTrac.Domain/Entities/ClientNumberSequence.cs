namespace PhysioTrac.Domain.Entities;

/// <summary>Single-row table used as a lock for monotonic client numbers.
/// Always has exactly one row, Id = 1.</summary>
public class ClientNumberSequence
{
    public short Id { get; set; } = 1;
    public long NextNumber { get; set; } = 1000;
}

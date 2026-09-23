using System.ComponentModel.DataAnnotations;

namespace PhysioTrac.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>SQL Server rowversion (optimistic concurrency token). The
    /// [Timestamp] attribute is EF Core's built-in convention for this --
    /// no per-entity DbContext configuration needed, and no application
    /// code ever sets this; the database generates and updates it on every
    /// write. A concurrent edit to the same row (e.g. two staff members
    /// editing one patient's chart at once) now fails the second writer's
    /// SaveChanges with DbUpdateConcurrencyException instead of silently
    /// overwriting the first writer's change.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

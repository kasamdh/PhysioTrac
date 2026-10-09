namespace PhysioTrac.Domain.Common;

/// <summary>Records who created and who last updated a row (the "when" is
/// BaseEntity.CreatedAt/UpdatedAt, UTC). Set automatically on save by
/// UserStampInterceptor from the signed-in user; null for system writes
/// (seeding, background jobs).</summary>
public interface IUserStamped
{
    Guid? CreatedById { get; set; }
    Guid? UpdatedById { get; set; }
}

/// <summary>A row that belongs to one clinical note's content. The
/// DbContext refuses to add, change or delete it once that note has left
/// Draft / Returned for correction -- signed charting is immutable.</summary>
public interface INoteOwned
{
    Guid NoteId { get; }
}

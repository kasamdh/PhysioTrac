using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Correction to a signed note; never changes the signed original.</summary>
public class NoteAddendum : BaseEntity
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid AuthorId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

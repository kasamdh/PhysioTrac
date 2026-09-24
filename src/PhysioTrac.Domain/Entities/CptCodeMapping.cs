using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>An organization's own choice of which CPT code represents a
/// given treatment category -- what makes ChargeService.GenerateFromNoteAsync
/// possible at all. NoteIntervention deliberately records a clinical
/// <see cref="InterventionCategory"/>, never a CPT code directly (see that
/// entity's own doc comment: "excluded from CPT-code suggestions rather
/// than guessed from free text") -- this row is the org-configurable bridge
/// between the two, since two clinics can reasonably bill the same
/// clinical activity under different codes.</summary>
public class CptCodeMapping : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public InterventionCategory InterventionCategory { get; set; }
    public string CptCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }
}

using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Read-only CPT/HCPCS reference data, shared across every tenant --
/// the CPT-side twin of <see cref="DiagnosisCode"/> (ICD-10), same
/// treatment: a shared catalog maintained only via a seed process, never
/// edited through the API.</summary>
public class CptCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this code is billed per timed minute (subject to the
    /// 8-minute rule, e.g. 97110 therapeutic exercise) versus a fixed
    /// service-based code billed as one unit regardless of time (e.g. 97161
    /// evaluation). Drives whether ChargeService.GenerateFromNoteAsync
    /// treats this code's grouped NoteIntervention minutes as
    /// unit-computing input at all.</summary>
    public bool IsTimeBased { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

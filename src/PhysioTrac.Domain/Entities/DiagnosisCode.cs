using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Read-only ICD-10-CM reference data, shared across every tenant —
/// the same catalog for everyone, not per-organization data. Maintained only
/// via a seed process, never edited through the API.</summary>
public class DiagnosisCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBillable { get; set; } = true;
}

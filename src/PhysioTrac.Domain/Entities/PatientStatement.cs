using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>A generated patient billing statement. Only the generation
/// event and a balance snapshot are stored — itemized charges/payments are
/// rebuilt live from Claim/Charge/ClaimTransaction data at view/print time,
/// filtered to activity on or before <see cref="StatementDate"/> so a
/// previously generated statement's line items stay reproducible.</summary>
public class PatientStatement : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateOnly StatementDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly DueDate { get; set; }
    public decimal BalanceAtGeneration { get; set; }
    public Guid? GeneratedById { get; set; }
}

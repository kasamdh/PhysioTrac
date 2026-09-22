using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One ledger entry against an insurance claim — a payment posted
/// manually, a contractual adjustment, a write-off, a refund, or a balance
/// transfer to a secondary claim. `ClaimId` is nullable to model an
/// unmatched payment sitting in a reconciliation queue until a biller
/// matches it. Cash-pay payments keep using <see cref="PaymentRecord"/>/
/// <see cref="Superbill"/> — this model is insurance-claim-ledger only.</summary>
public class ClaimTransaction : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid? ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public Guid? TransferredToClaimId { get; set; }
    public Claim? TransferredToClaim { get; set; }

    public ClaimTransactionKind Kind { get; set; }
    public ClaimTransactionMethod? Method { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string? Reference { get; set; }
    public string? DenialCode { get; set; }
    public string? DenialReason { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedById { get; set; }

    public bool IsMatched => ClaimId is not null;
}

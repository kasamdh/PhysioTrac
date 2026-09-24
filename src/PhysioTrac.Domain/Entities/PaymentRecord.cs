using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Token/reference-only payment record; never captures cardholder
/// data here.</summary>
public class PaymentRecord : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid? SuperbillId { get; set; }
    public Superbill? Superbill { get; set; }

    public Guid RecordedById { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ReceivedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Pending;
    public string PaymentProcessorReference { get; set; } = string.Empty;

    /// <summary>How this cash-pay payment was actually collected -- recorded
    /// only, matching this app's "no live payment processing" posture even
    /// for a card/payment-plan entry (see ClaimTransactionMethod, reused
    /// here rather than a duplicate enum since the same method vocabulary
    /// applies to both insurance-side and cash-pay collections).</summary>
    public ClaimTransactionMethod? Method { get; set; }
}

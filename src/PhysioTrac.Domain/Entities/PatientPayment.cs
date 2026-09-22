using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A patient-initiated online payment attempt through the portal —
/// distinct from <see cref="PaymentRecord"/> (staff manually recording a
/// payment already collected out-of-band) and <see cref="ClaimTransaction"/>
/// (insurance-ledger posting). Every attempt is kept, succeeded or not, for
/// a complete patient-facing payment history — insert-only. Never stores
/// card/CVV data.
///
/// Deliberately omits the original's `mobile_care_request` FK — that
/// entity isn't ported yet.</summary>
public class PatientPayment : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public decimal Amount { get; set; }
    public PatientPaymentStatus Status { get; set; } = PatientPaymentStatus.Pending;
    public string? ProcessorReference { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
}

using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>A payer's contracted/allowed amount for one CPT code --
/// distinct from <see cref="ServicePrice"/> (what the clinic bills) the same
/// way a claim's billed charge amount is distinct from what the payer
/// actually allows: you still bill your standard fee schedule, but the
/// payer's allowed amount is what reporting/variance analysis compares
/// actual reimbursement against. Nothing in ChargeService/ClaimService
/// enforces this as a cap -- it's informational/reporting data, matching
/// this app's already-established "data-driven, not hard-coded, billing
/// staff maintain it themselves" treatment of <see cref="Payer"/>.</summary>
public class PayerFeeScheduleItem : BaseEntity
{
    public Guid PayerId { get; set; }
    public Payer? Payer { get; set; }

    public string CptCode { get; set; } = string.Empty;
    public decimal AllowedAmount { get; set; }
    public Guid CreatedById { get; set; }
}

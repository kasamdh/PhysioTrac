namespace PhysioTrac.Domain.Enums;

public enum ClaimStatus
{
    Draft,
    Ready,
    ValidationError,
    Submitted,
    Accepted,
    Rejected,
    Processing,
    Denied,
    PartialPayment,
    Paid,
    Appealed,
    Corrected,
    Closed,
}

public enum ClaimTransactionKind
{
    InsurancePayment,
    PatientPayment,
    Adjustment,
    WriteOff,
    Refund,
    Transfer,
}

public enum ClaimTransactionMethod
{
    Check,
    Eft,
    CreditCard,
    Cash,
    PaymentPlan,
    Era,
    Other,
}

public enum AppealStatus
{
    NotAppealed,
    Preparing,
    Submitted,
    Won,
    Lost,
}

public enum DenialResolution
{
    Open,
    ResolvedPaid,
    ResolvedWrittenOff,
    ResolvedPatientBilled,
}

public enum SuperbillStatus
{
    Draft,
    Ready,
    Submitted,
    Paid,
}

public enum PaymentRecordStatus
{
    Pending,
    Received,
    Refunded,
    Void,
}

public enum PatientPaymentStatus
{
    Pending,
    Succeeded,
    Failed,
}

/// <summary>Which timed-minutes-to-units table a charge's recommended
/// units are computed from -- see EightMinuteRuleCalculator. Configurable
/// per organization (Organization.EightMinuteRuleVariant) since not every
/// payer follows the Medicare table; either way the result is always
/// labeled a suggestion, never submitted without a human able to override
/// it (Charge.UnitsOverrideReason).</summary>
public enum EightMinuteRuleVariant
{
    /// <summary>Standard CMS lookup: 8-22=1, 23-37=2, 38-52=3, 53-67=4,
    /// +1 per additional 15 minutes.</summary>
    Medicare,

    /// <summary>A simpler alternative some commercial payers/clinics use
    /// instead of Medicare's table: one unit per full or majority 15-minute
    /// increment (round-half-up), with a minimum of 1 unit once the
    /// 8-minute threshold is met. This is a simplification, not a specific
    /// payer's published rule -- always confirm against an individual
    /// payer's actual contract before relying on it.</summary>
    RoundedFifteenMinute,
}

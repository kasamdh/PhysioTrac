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

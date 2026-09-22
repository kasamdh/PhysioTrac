using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Billing;

public record ClaimTotalsDto(decimal TotalChargeAmount, decimal TotalPaid, decimal TotalAdjusted, decimal Balance);

public record ClaimDto(
    Guid Id, Guid PatientId, Guid PatientInsuranceId, Guid PayerId, IReadOnlyList<string> DiagnosisCodeList,
    ClaimStatus Status, string? ClearinghouseClaimId, DateTimeOffset? SubmittedAt, DateTimeOffset? ClosedAt, ClaimTotalsDto Totals);

public record CreateClaimRequest(Guid PatientId, Guid PatientInsuranceId, IReadOnlyList<Guid> ChargeIds);

public record UpdateClaimStatusRequest(ClaimStatus Status);

public record RecordClaimTransactionRequest(
    Guid PatientId, Guid? ClaimId, Guid? TransferredToClaimId, ClaimTransactionKind Kind, ClaimTransactionMethod? Method,
    decimal Amount, DateOnly? PaymentDate, string? Reference, string? DenialCode, string? DenialReason, string? Notes);

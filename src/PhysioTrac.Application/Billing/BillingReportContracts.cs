namespace PhysioTrac.Application.Billing;

public record AgingBucketDto(decimal Days0To30, decimal Days31To60, decimal Days61To90, decimal Days90Plus, decimal Total);

/// <summary>Outstanding-balance aging, bucketed by days since the earliest
/// date of service on the underlying charges -- the same denominator for
/// both halves, since an insurance claim's own SubmittedAt can be null
/// (still Draft) while it's already accumulating patient-facing age.
/// Separated into insurance (Claim) and cash-pay (Superbill) since they're
/// two entirely separate billing paths in this app (a Charge belongs to
/// one or the other, never both) and mixing them would hide which
/// collection process is actually falling behind.</summary>
public record AgingReportDto(AgingBucketDto InsuranceClaims, AgingBucketDto CashPaySuperbills, AgingBucketDto Combined);

public record PatientBalanceDto(Guid PatientId, decimal OpenClaimsBalance, decimal OpenSuperbillsBalance, decimal TotalBalance);

public enum RevenueGroupBy
{
    Provider,
    Location,
    Service,
}

public record RevenueReportFilter(DateOnly From, DateOnly To, Guid? ProviderId, Guid? LocationId, string? CptCode, RevenueGroupBy GroupBy);

public record RevenueReportRowDto(string Key, decimal BilledAmount, int ChargeCount);

/// <summary>Billed amount is accrual (grouped by the requested dimension,
/// filtered to charges with a service date in range); TotalCollected is
/// cash actually received in the same date range regardless of which
/// visit's charge it pays down -- a deliberate accrual-vs-cash split real
/// billing reports draw, not an attempt to tie every dollar collected back
/// to one grouped row.</summary>
public record RevenueReportDto(DateOnly From, DateOnly To, decimal TotalBilled, decimal TotalCollected, IReadOnlyList<RevenueReportRowDto> Rows);

public record PatientStatementLineItemDto(DateOnly Date, string Kind, string Description, decimal Amount);

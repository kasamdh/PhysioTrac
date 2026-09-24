using PhysioTrac.Application.Auth;

namespace PhysioTrac.Application.Billing;

/// <summary>Read-only aggregation over Charge/Claim/ClaimTransaction/
/// Superbill/PaymentRecord -- no new persisted state of its own, everything
/// computed live from the existing billing ledger.</summary>
public interface IBillingReportService
{
    Task<PatientBalanceDto> GetPatientBalanceAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    Task<AgingReportDto> GetAgingReportAsync(ICurrentUser actor, CancellationToken ct = default);

    Task<RevenueReportDto> GetRevenueReportAsync(RevenueReportFilter filter, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Reconstructs a previously generated PatientStatement's line
    /// items live -- charges and payment/adjustment activity for this
    /// patient dated on or before the statement's own StatementDate -- so a
    /// statement stays reproducible after later activity accrues, matching
    /// PatientStatement's own documented "rebuilt live... not ported yet"
    /// simplification (this is that missing piece).</summary>
    Task<IReadOnlyList<PatientStatementLineItemDto>> GetStatementLineItemsAsync(Guid statementId, ICurrentUser actor, CancellationToken ct = default);
}

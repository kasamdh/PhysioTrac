using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;

namespace PhysioTrac.Api.Controllers;

/// <summary>Read-only billing aggregation -- aging and revenue-by-dimension.
/// Everything computed live from Charge/Claim/ClaimTransaction/Superbill/
/// PaymentRecord; nothing new is persisted here.</summary>
[ApiController]
[Route("api/v1/billing-reports")]
[Authorize]
public class BillingReportsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IBillingReportService _reports;

    public BillingReportsController(ICurrentUser currentUser, IBillingReportService reports)
    {
        _currentUser = currentUser;
        _reports = reports;
    }

    [HttpGet("patient-balance/{patientId:guid}")]
    public async Task<IActionResult> PatientBalance(Guid patientId)
    {
        try
        {
            var balance = await _reports.GetPatientBalanceAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(balance);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("aging")]
    public async Task<IActionResult> Aging()
    {
        try
        {
            var report = await _reports.GetAgingReportAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(report);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] RevenueGroupBy groupBy = RevenueGroupBy.Service,
        [FromQuery] Guid? providerId = null, [FromQuery] Guid? locationId = null, [FromQuery] string? cptCode = null)
    {
        try
        {
            var report = await _reports.GetRevenueReportAsync(
                new RevenueReportFilter(from, to, providerId, locationId, cptCode, groupBy), _currentUser, HttpContext.RequestAborted);
            return Ok(report);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}

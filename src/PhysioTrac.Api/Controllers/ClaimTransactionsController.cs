using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/claim-transactions")]
[Authorize]
public class ClaimTransactionsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IClaimTransactionService _transactions;

    public ClaimTransactionsController(ICurrentUser currentUser, IClaimTransactionService transactions)
    {
        _currentUser = currentUser;
        _transactions = transactions;
    }

    [HttpGet("claim/{claimId:guid}")]
    public async Task<IActionResult> ListForClaim(Guid claimId)
    {
        try
        {
            return Ok(await _transactions.ListForClaimAsync(claimId, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordClaimTransactionRequest request)
    {
        try
        {
            var transaction = await _transactions.RecordAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForClaim), new { claimId = transaction.ClaimId }, transaction);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/patient-payments")]
[Authorize]
public class PatientPaymentsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public PatientPaymentsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db, IAuditService audit)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
        _audit = audit;
    }

    /// <summary>Staff-facing view of a patient's online payment history.</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var payments = await _db.PatientPayments.Where(p => p.PatientId == patient.Id)
                .OrderByDescending(p => p.AttemptedAt).ToListAsync(HttpContext.RequestAborted);
            return Ok(payments);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    /// <summary>Portal-only: a patient records their own payment attempt,
    /// succeeded or not, via `care/payment_processor.py`'s seam. Never
    /// accepts card/CVV data — only a processor reference.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Portal)]
    public async Task<IActionResult> Create([FromBody] CreatePatientPaymentRequest request)
    {
        try
        {
            var patient = await _tenantAccess.RequirePortalPatientAsync(_currentUser, HttpContext.RequestAborted);
            if (request.Amount <= 0)
            {
                return UnprocessableEntity(new { detail = "Enter an amount greater than zero." });
            }

            var payment = new PatientPayment
            {
                PatientId = patient.Id,
                Amount = request.Amount,
                Status = request.Status,
                ProcessorReference = request.ProcessorReference,
                FailureMessage = request.FailureMessage,
            };
            _db.PatientPayments.Add(payment);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "patient_payment.attempted", nameof(PatientPayment), payment.Id,
                organization.Id, patientId: patient.Id, metadata: new { amount = payment.Amount, status = payment.Status.ToString() }, ct: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(ListForPatient), new { patientId = patient.Id }, payment);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record CreatePatientPaymentRequest(decimal Amount, PhysioTrac.Domain.Enums.PatientPaymentStatus Status, string? ProcessorReference, string? FailureMessage);

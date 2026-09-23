using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/payment-records")]
[Authorize]
public class PaymentRecordsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public PaymentRecordsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, patientId, ct: HttpContext.RequestAborted);
            var payments = await _db.PaymentRecords.Where(p => p.PatientId == patient.Id)
                .OrderByDescending(p => p.ReceivedOn).ToListAsync(HttpContext.RequestAborted);
            return Ok(payments);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRecordRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.PaymentCollection);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, request.PatientId, ct: HttpContext.RequestAborted);

            if (request.SuperbillId is Guid superbillId)
            {
                var superbill = await _db.Superbills.FirstOrDefaultAsync(s => s.Id == superbillId, HttpContext.RequestAborted);
                if (superbill is null || superbill.PatientId != patient.Id)
                {
                    return UnprocessableEntity(new { detail = "A payment can only be linked to this patient's superbill." });
                }
            }

            var payment = new PaymentRecord
            {
                PatientId = patient.Id,
                SuperbillId = request.SuperbillId,
                RecordedById = _currentUser.UserId,
                Amount = request.Amount,
                ReceivedOn = request.ReceivedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentProcessorReference = request.PaymentProcessorReference,
            };
            _db.PaymentRecords.Add(payment);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForPatient), new { patientId = patient.Id }, payment);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record CreatePaymentRecordRequest(Guid PatientId, Guid? SuperbillId, decimal Amount, DateOnly? ReceivedOn, string PaymentProcessorReference);

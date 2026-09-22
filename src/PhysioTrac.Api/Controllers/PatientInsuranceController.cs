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
[Route("api/v1/patient-insurance")]
[Authorize]
public class PatientInsuranceController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public PatientInsuranceController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var policies = await _db.PatientInsurancePolicies.Where(i => i.PatientId == patient.Id)
                .OrderBy(i => i.Rank).ThenByDescending(i => i.EffectiveDate)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(policies);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePatientInsuranceRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Billing);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, request.PatientId, ct: HttpContext.RequestAborted);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var payer = await _db.Payers.FirstOrDefaultAsync(p => p.Id == request.PayerId && p.OrganizationId == organization.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Payer was not found.");
            if (request.TerminationDate is DateOnly termination && termination < request.EffectiveDate)
            {
                return UnprocessableEntity(new { detail = "Termination date cannot be before the effective date." });
            }
            if (request.CoinsurancePercent is decimal coinsurance && (coinsurance < 0 || coinsurance > 100))
            {
                return UnprocessableEntity(new { detail = "Coinsurance must be between 0 and 100 percent." });
            }

            var policy = new PatientInsurance
            {
                OrganizationId = organization.Id,
                PatientId = patient.Id,
                PayerId = payer.Id,
                Rank = request.Rank,
                PlanName = request.PlanName,
                MemberId = request.MemberId,
                GroupNumber = request.GroupNumber,
                SubscriberName = request.SubscriberName,
                SubscriberDateOfBirth = request.SubscriberDateOfBirth,
                RelationshipToSubscriber = request.RelationshipToSubscriber,
                EffectiveDate = request.EffectiveDate,
                TerminationDate = request.TerminationDate,
                Copay = request.Copay,
                CoinsurancePercent = request.CoinsurancePercent,
                Deductible = request.Deductible,
                AuthorizationRequired = request.AuthorizationRequired,
                CreatedById = _currentUser.UserId,
            };
            _db.PatientInsurancePolicies.Add(policy);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListForPatient), new { patientId = patient.Id }, policy);
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }
}

public record CreatePatientInsuranceRequest(
    Guid PatientId, Guid PayerId, PhysioTrac.Domain.Enums.InsuranceRank Rank, string? PlanName, string MemberId,
    string? GroupNumber, string? SubscriberName, DateOnly? SubscriberDateOfBirth,
    PhysioTrac.Domain.Enums.RelationshipToSubscriber RelationshipToSubscriber,
    DateOnly EffectiveDate, DateOnly? TerminationDate, decimal? Copay, decimal? CoinsurancePercent, decimal? Deductible, bool AuthorizationRequired);

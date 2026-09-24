using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Application.Intake;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

/// <summary>Cross-cutting patient-portal self-service, distinct from
/// PortalBookingController (appointments/waitlist specifically). Every
/// action resolves "which patient" via
/// ITenantAccessService.RequirePortalPatientAsync -- never from a
/// client-supplied patient id -- so a patient can never reach another
/// patient's data by guessing or tampering with an id, even within their
/// own organization.
///
/// This is also the answer to "a separate login flow": there isn't a
/// technically distinct login endpoint (Patient accounts sign in through
/// the exact same cookie+CSRF POST /api/v1/auth/login as every other role,
/// by design -- see this session's standing architecture decisions), but a
/// portal frontend has no other way to learn its own linked patient id, so
/// GET /me is that bootstrap call.</summary>
[ApiController]
[Route("api/v1/portal")]
[Authorize]
public class PortalController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IConsentService _consents;
    private readonly IIntakeFormService _forms;

    public PortalController(ICurrentUser currentUser, ITenantAccessService tenantAccess, IConsentService consents, IIntakeFormService forms)
    {
        _currentUser = currentUser;
        _tenantAccess = tenantAccess;
        _consents = consents;
        _forms = forms;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try
        {
            var patient = await _tenantAccess.RequirePortalPatientAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(new PortalPatientDto(patient.Id, patient.FirstName, patient.LastName, patient.OrganizationId));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost("consents")]
    public async Task<IActionResult> SignConsent([FromBody] RecordOwnConsentRequest request)
    {
        try
        {
            var consent = await _consents.RecordOwnAsync(_currentUser, request, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted);
            return StatusCode(201, new ConsentDto(
                consent.Id, consent.PatientId, consent.ConsentType, consent.SignedByName,
                consent.RecordedById, consent.SignedAt, consent.IsActive, consent.RevokedAt, consent.TemplateVersion));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("intake-form-templates")]
    public async Task<IActionResult> ListIntakeFormTemplates()
    {
        try
        {
            var patient = await _tenantAccess.RequirePortalPatientAsync(_currentUser, HttpContext.RequestAborted);
            var templates = await _forms.ListResolvedForOrganizationAsync(_currentUser, patient.PrimaryLocationId, null, HttpContext.RequestAborted);
            return Ok(templates.Select(ToTemplateDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost("intake-form-submissions")]
    public async Task<IActionResult> SubmitIntakeForm([FromBody] SubmitIntakeFormRequest request)
    {
        try
        {
            var submission = await _forms.SubmitAsync(_currentUser, request, HttpContext.RequestAborted);
            return StatusCode(201, ToSubmissionDto(submission));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("intake-form-submissions")]
    public async Task<IActionResult> ListMyIntakeFormSubmissions()
    {
        try
        {
            var patient = await _tenantAccess.RequirePortalPatientAsync(_currentUser, HttpContext.RequestAborted);
            var submissions = await _forms.ListSubmissionsForPatientAsync(patient.Id, _currentUser, HttpContext.RequestAborted);
            return Ok(submissions.Select(ToSubmissionDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    private static IntakeFormTemplateDto ToTemplateDto(IntakeFormTemplate t) => new(
        t.Id, t.Key, t.Scope, t.State, t.LocationId, t.Name, t.SchemaJson, t.Version, t.IsActive);

    private static IntakeFormSubmissionDto ToSubmissionDto(IntakeFormSubmission s) => new(
        s.Id, s.PatientId, s.IntakeFormTemplateId, s.TemplateVersion, s.ResponseJson, s.Status, s.SubmittedAt, s.ReviewedById, s.ReviewedAt);
}

public record PortalPatientDto(Guid PatientId, string FirstName, string LastName, Guid OrganizationId);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Intake;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

/// <summary>Staff-facing view of a patient's submitted intake forms, plus
/// the review action. Submission itself is patient-portal-only -- see
/// PortalController.SubmitIntakeForm, which never accepts a client-supplied
/// patient id.</summary>
[ApiController]
[Route("api/v1/patients/{patientId:guid}/intake-form-submissions")]
[Authorize]
public class IntakeFormSubmissionsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IIntakeFormService _forms;

    public IntakeFormSubmissionsController(ICurrentUser currentUser, IIntakeFormService forms)
    {
        _currentUser = currentUser;
        _forms = forms;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientId)
    {
        try
        {
            var submissions = await _forms.ListSubmissionsForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(submissions.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost("{submissionId:guid}/review")]
    public async Task<IActionResult> Review(Guid patientId, Guid submissionId)
    {
        try
        {
            var submission = await _forms.ReviewAsync(submissionId, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(submission));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    private static IntakeFormSubmissionDto ToDto(IntakeFormSubmission s) => new(
        s.Id, s.PatientId, s.IntakeFormTemplateId, s.TemplateVersion, s.ResponseJson, s.Status, s.SubmittedAt, s.ReviewedById, s.ReviewedAt);
}

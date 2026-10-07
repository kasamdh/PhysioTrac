using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IClinicalNoteService _notes;

    public NotesController(ICurrentUser currentUser, IClinicalNoteService notes)
    {
        _currentUser = currentUser;
        _notes = notes;
    }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<IActionResult> ListForPatient(Guid patientId)
    {
        try
        {
            var notes = await _notes.ListForPatientAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(notes.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var note = await _notes.GetAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request)
    {
        try
        {
            var note = await _notes.CreateDraftAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = note.Id }, ToDto(note));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request)
    {
        try
        {
            var note = await _notes.UpdateDraftAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("patient/{patientId:guid}/pull-forward")]
    public async Task<IActionResult> PullForward(Guid patientId)
    {
        try
        {
            var data = await _notes.GetPullForwardDataAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(data);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpGet("patient/{patientId:guid}/progress-note-status")]
    public async Task<IActionResult> ProgressNoteStatus(Guid patientId)
    {
        try
        {
            var status = await _notes.GetProgressNoteStatusAsync(patientId, _currentUser, HttpContext.RequestAborted);
            return Ok(status);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}/compliance")]
    public async Task<IActionResult> Compliance(Guid id)
    {
        try
        {
            var note = await _notes.GetAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(NoteComplianceEvaluator.Evaluate(note));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/sign")]
    public async Task<IActionResult> Sign(Guid id, [FromBody] SignNoteRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var note = await _notes.SignNoteAsync(id, request.AttestationConfirmed, ipAddress, _currentUser, request.Password, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (SignatureVerificationException ex) { return UnprocessableEntity(new { detail = ex.Message, code = "SIGNATURE_PASSWORD" }); }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        try
        {
            var note = await _notes.LockNoteAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/certify-poc")]
    public async Task<IActionResult> CertifyPlanOfCare(Guid id, [FromBody] CertifyPlanOfCareRequest request)
    {
        try
        {
            var note = await _notes.CertifyPlanOfCareAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id)
    {
        try
        {
            return Ok(await _notes.GetVersionHistoryViewAsync(id, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/cosign")]
    public async Task<IActionResult> Cosign(Guid id, [FromBody] CosignNoteRequest? request)
    {
        try
        {
            var note = await _notes.CosignNoteAsync(id, _currentUser, request?.Password, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (SignatureVerificationException ex) { return UnprocessableEntity(new { detail = ex.Message, code = "SIGNATURE_PASSWORD" }); }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/addenda")]
    public async Task<IActionResult> CreateAddendum(Guid id, [FromBody] CreateAddendumRequest request)
    {
        try
        {
            var addendum = await _notes.CreateAddendumAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id }, new NoteAddendumDto(
                addendum.Id, addendum.NoteId, addendum.AuthorId, addendum.Reason, addendum.Body, addendum.CreatedAt));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    /// <summary>Starts (or continues) a formal amendment of a signed note;
    /// returns the amendment draft to edit and sign.</summary>
    [HttpPost("{id:guid}/amend")]
    public async Task<IActionResult> Amend(Guid id, [FromBody] CreateAmendmentRequest request)
    {
        try
        {
            var amendment = await _notes.CreateAmendmentAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(amendment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    /// <summary>What the current user may do with the note, plus its author,
    /// cosigner, addenda and amendment link.</summary>
    [HttpGet("{id:guid}/record")]
    public async Task<IActionResult> Record(Guid id)
    {
        try
        {
            return Ok(await _notes.GetNoteRecordAsync(id, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    /// <summary>The current user's unsigned notes and notes awaiting their cosignature.</summary>
    [HttpGet("queues")]
    public async Task<IActionResult> Queues()
    {
        try
        {
            return Ok(await _notes.GetNoteQueuesAsync(_currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}/interventions")]
    public async Task<IActionResult> ListInterventions(Guid id)
    {
        try
        {
            var items = await _notes.ListInterventionsAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(items.Select(ToInterventionDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/interventions")]
    public async Task<IActionResult> AddIntervention(Guid id, [FromBody] CreateInterventionRequest request)
    {
        try
        {
            var item = await _notes.AddInterventionAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListInterventions), new { id }, ToInterventionDto(item));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPut("{id:guid}/interventions/{interventionId:guid}")]
    public async Task<IActionResult> UpdateIntervention(Guid id, Guid interventionId, [FromBody] CreateInterventionRequest request)
    {
        try
        {
            var item = await _notes.UpdateInterventionAsync(id, interventionId, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToInterventionDto(item));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpDelete("{id:guid}/interventions/{interventionId:guid}")]
    public async Task<IActionResult> DeleteIntervention(Guid id, Guid interventionId)
    {
        try
        {
            await _notes.DeleteInterventionAsync(id, interventionId, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}/interventions/summary")]
    public async Task<IActionResult> InterventionSummary(Guid id)
    {
        try
        {
            return Ok(await _notes.SummarizeInterventionsAsync(id, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private static ClinicalNoteDto ToDto(ClinicalNote n) => new(
        n.Id, n.PatientId, n.TherapistId, n.AppointmentId, n.NoteType, n.Status, n.ServiceDate,
        n.Subjective, n.Objective, n.Interventions, n.Assessment, n.Plan,
        n.PlanOfCareStart, n.PlanOfCareEnd, n.FrequencyPerWeek, n.DurationWeeks, n.ReassessmentDue,
        n.PlanOfCareCertifiedDate, n.PlanOfCareCertifyingProviderId,
        n.SignatureName, n.SignatureCredentials, n.SignedAt, n.SignatureIpAddress, n.SignatureHash,
        n.CosignRequired, n.CosignedById, n.CosignedAt,
        n.SubjectiveDetailsJson, n.ObjectiveMeasurementsJson, n.AmendsNoteId, n.AmendmentReason);

    private static InterventionDto ToInterventionDto(NoteIntervention i) => new(
        i.Id, i.NoteId, i.Description, i.BodyRegion, i.Category, i.Minutes, i.Units, i.IsTimed, i.Order, i.PatientResponse);
}

/// <param name="Password">The signer re-enters their own password (step-up).</param>
public record SignNoteRequest(bool AttestationConfirmed, string? Password = null);

/// <param name="Password">The cosigner re-enters their own password (step-up).</param>
public record CosignNoteRequest(string? Password = null);

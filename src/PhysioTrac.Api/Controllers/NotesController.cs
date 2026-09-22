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
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var note = await _notes.GetAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
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
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request)
    {
        try
        {
            var note = await _notes.UpdateDraftAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
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
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/sign")]
    public async Task<IActionResult> Sign(Guid id, [FromBody] SignNoteRequest request)
    {
        try
        {
            var note = await _notes.SignNoteAsync(id, request.AttestationConfirmed, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/cosign")]
    public async Task<IActionResult> Cosign(Guid id)
    {
        try
        {
            var note = await _notes.CosignNoteAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(note));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
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
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}/interventions")]
    public async Task<IActionResult> ListInterventions(Guid id)
    {
        try
        {
            var items = await _notes.ListInterventionsAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(items.Select(ToInterventionDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
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
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    private static ClinicalNoteDto ToDto(ClinicalNote n) => new(
        n.Id, n.PatientId, n.TherapistId, n.AppointmentId, n.NoteType, n.Status, n.ServiceDate,
        n.Subjective, n.Objective, n.Interventions, n.Assessment, n.Plan,
        n.PlanOfCareStart, n.PlanOfCareEnd, n.FrequencyPerWeek, n.DurationWeeks, n.ReassessmentDue,
        n.SignatureName, n.SignedAt, n.CosignRequired, n.CosignedById, n.CosignedAt);

    private static InterventionDto ToInterventionDto(NoteIntervention i) => new(
        i.Id, i.NoteId, i.Description, i.BodyRegion, i.Category, i.Minutes, i.Units, i.IsTimed, i.Order);
}

public record SignNoteRequest(bool AttestationConfirmed);

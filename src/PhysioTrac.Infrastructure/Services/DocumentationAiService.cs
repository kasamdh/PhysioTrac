using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>AI-assisted drafting for notes. Builds a de-identified context
/// from the encounter, asks the configured provider for text, and hands it
/// back as a suggestion -- it never writes to the note, and has no path to
/// signing. Audit rows record the section and provider only, never text.</summary>
public class DocumentationAiService : IDocumentationAiService
{
    private readonly IClinicalNoteService _notes;
    private readonly IDocumentationAiProvider? _provider;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public DocumentationAiService(IClinicalNoteService notes, ITenantAccessService tenantAccess, IAuditService audit,
        IDocumentationAiProvider? provider = null)
    {
        _notes = notes;
        _tenantAccess = tenantAccess;
        _audit = audit;
        _provider = provider;
    }

    public AiStatusDto GetStatus() => new(_provider is not null, _provider?.Name ?? "None");

    public async Task<AiDraftDto> DraftAsync(Guid noteId, AiDraftSection section, ICurrentUser actor, CancellationToken ct = default)
    {
        var provider = _provider ?? throw new InvalidOperationException("AI drafting is turned off.");
        if (!Enum.IsDefined(section)) throw new InvalidOperationException("AI can draft the assessment or the plan only.");
        var note = await RequireEditableAsync(noteId, actor, ct);

        var encounter = await _notes.GetEncounterAsync(noteId, actor, ct);
        var text = (await provider.DraftAsync(section, AiRules.ContextFrom(encounter), ct)).Trim();

        await AuditAsync(note, AiRules.DraftedAction, section, provider.Name, actor, ct);
        return new AiDraftDto(section, text, provider.Name, DateTimeOffset.UtcNow, AiRules.Notice);
    }

    public async Task RecordInsertedAsync(Guid noteId, AiDraftSection section, ICurrentUser actor, CancellationToken ct = default)
    {
        var provider = _provider ?? throw new InvalidOperationException("AI drafting is turned off.");
        if (!Enum.IsDefined(section)) throw new InvalidOperationException("Unknown section.");
        var note = await RequireEditableAsync(noteId, actor, ct);
        await AuditAsync(note, AiRules.InsertedAction, section, provider.Name, actor, ct);
    }

    private async Task<ClinicalNote> RequireEditableAsync(Guid noteId, ICurrentUser actor, CancellationToken ct)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var note = await _notes.GetAsync(noteId, actor, ct);
        if (!_notes.CanEditNote(actor, note) || !note.IsEditable)
            throw new ForbiddenException(note.IsEditable
                ? "You are not permitted to edit this note."
                : "AI drafting is only available while a note is being written.");
        return note;
    }

    private async Task AuditAsync(ClinicalNote note, string action, AiDraftSection section, string provider, ICurrentUser actor,
        CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, action, nameof(ClinicalNote), note.Id, organization.Id, patientId: note.PatientId,
            metadata: ClinicalNoteService.NoteAudit(note, organization, null, new { section = section.ToString(), provider }), ct: ct);
    }
}

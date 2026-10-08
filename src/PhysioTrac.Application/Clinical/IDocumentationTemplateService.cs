using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>Configurable documentation templates: system templates shipped
/// with the application plus the clinic's own, each with immutable
/// versions. Clinical staff read and use them; administrators and directors
/// manage them.</summary>
public interface IDocumentationTemplateService
{
    Task<IReadOnlyList<DocumentationTemplateDto>> ListAsync(ICurrentUser actor, NoteType? noteType = null,
        ClinicalSpecialty? specialty = null, bool includeInactive = false, string? search = null, CancellationToken ct = default);

    /// <summary>The template with its current version.</summary>
    Task<DocumentationTemplateDetailDto> GetAsync(Guid templateId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>One exact version -- what a note written with it renders.</summary>
    Task<TemplateVersionDto> GetVersionAsync(Guid versionId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<TemplateVersionSummaryDto>> ListVersionsAsync(Guid templateId, ICurrentUser actor, CancellationToken ct = default);

    Task<DocumentationTemplateDetailDto> CreateAsync(SaveDocumentationTemplateRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Saves an edit: name, specialty, description and appointment
    /// types update in place; a change to sections or fields publishes a new
    /// version. System templates can't be edited -- copy them.</summary>
    Task<DocumentationTemplateDetailDto> UpdateAsync(Guid templateId, SaveDocumentationTemplateRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<DocumentationTemplateDto> SetActiveAsync(Guid templateId, bool isActive, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>A clinic copy of any template (version 1 = the source's current version).</summary>
    Task<DocumentationTemplateDetailDto> CopyAsync(Guid templateId, CopyTemplateRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task SetFavoriteAsync(Guid templateId, bool favorite, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The template a new note should start with: one assigned to
    /// the appointment type, then the user's favorite, then the specialty's,
    /// then the clinic's own, then the system default for the note type.</summary>
    Task<DocumentationTemplateDto?> SuggestAsync(ICurrentUser actor, NoteType noteType, Guid? appointmentTypeId = null,
        ClinicalSpecialty? specialty = null, CancellationToken ct = default);
}

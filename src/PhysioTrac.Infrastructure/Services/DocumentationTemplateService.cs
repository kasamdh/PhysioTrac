using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class DocumentationTemplateService : IDocumentationTemplateService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public DocumentationTemplateService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    // ------------------------------------------------------------------ reads

    public async Task<IReadOnlyList<DocumentationTemplateDto>> ListAsync(ICurrentUser actor, NoteType? noteType = null,
        ClinicalSpecialty? specialty = null, bool includeInactive = false, string? search = null, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var query = Visible(organization.Id);
        if (noteType is not null) query = query.Where(t => t.NoteType == noteType);
        if (specialty is not null) query = query.Where(t => t.Specialty == specialty);
        if (!includeInactive) query = query.Where(t => t.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.Name.Contains(term) || (t.Description != null && t.Description.Contains(term)));
        }
        var templates = await query.Include(t => t.AppointmentTypes).ToListAsync(ct);
        return await ToDtosAsync(templates, actor, ct);
    }

    public async Task<DocumentationTemplateDetailDto> GetAsync(Guid templateId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var template = await LoadVisibleAsync(templateId, actor, ct);
        return await DetailAsync(template, actor, ct);
    }

    public async Task<TemplateVersionDto> GetVersionAsync(Guid versionId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var version = await _db.ClinicalNoteTemplateVersions.AsNoTracking()
            .Include(v => v.Sections).Include(v => v.Fields).Include(v => v.Template)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("Template version was not found.");
        if (version.Template!.OrganizationId is Guid owner && owner != organization.Id)
            throw new NotFoundException("Template version was not found.");
        return await ToVersionDtoAsync(version, ct);
    }

    public async Task<IReadOnlyList<TemplateVersionSummaryDto>> ListVersionsAsync(Guid templateId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var template = await LoadVisibleAsync(templateId, actor, ct);
        var versions = await _db.ClinicalNoteTemplateVersions.AsNoTracking().Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.Id, v.VersionNumber, v.ChangeSummary, v.CreatedAt, v.CreatedById })
            .ToListAsync(ct);
        var ids = versions.Select(v => v.Id).ToList();
        var usage = await _db.ClinicalNotes.Where(n => n.TemplateVersionId != null && ids.Contains(n.TemplateVersionId.Value))
            .GroupBy(n => n.TemplateVersionId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var names = await NamesAsync(versions.Where(v => v.CreatedById is not null).Select(v => v.CreatedById!.Value), ct);
        return versions.Select(v => new TemplateVersionSummaryDto(v.Id, v.VersionNumber, v.ChangeSummary, v.CreatedAt,
            v.CreatedById is Guid by ? names.GetValueOrDefault(by) : null,
            usage.FirstOrDefault(u => u.Key == v.Id)?.Count ?? 0)).ToList();
    }

    public async Task<DocumentationTemplateDto?> SuggestAsync(ICurrentUser actor, NoteType noteType, Guid? appointmentTypeId = null,
        ClinicalSpecialty? specialty = null, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var candidates = await Visible(organization.Id).Where(t => t.IsActive && t.NoteType == noteType)
            .Include(t => t.AppointmentTypes).ToListAsync(ct);
        if (candidates.Count == 0) return null;
        var favorites = await FavoriteIdsAsync(actor, ct);

        var best = candidates
            .OrderByDescending(t => appointmentTypeId is Guid at && t.AppointmentTypes.Any(a => a.AppointmentTypeId == at))
            .ThenByDescending(t => favorites.Contains(t.Id))
            .ThenByDescending(t => specialty is not null && t.Specialty == specialty)
            .ThenByDescending(t => !t.IsSystem)
            .ThenByDescending(t => t.Specialty == ClinicalSpecialty.General)
            .ThenBy(t => t.Name)
            .First();
        return (await ToDtosAsync([best], actor, ct))[0];
    }

    // ----------------------------------------------------------------- writes

    public async Task<DocumentationTemplateDetailDto> CreateAsync(SaveDocumentationTemplateRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        Validate(request);
        await ValidateAppointmentTypesAsync(request.AppointmentTypeIds, organization.Id, ct);

        var template = new ClinicalNoteTemplate
        {
            OrganizationId = organization.Id,
            Scope = TemplateScope.Organization,
            Name = request.Name.Trim(),
            NoteType = request.NoteType,
            Specialty = request.Specialty,
            Description = Trim(request.Description),
            Version = 1,
            CreatedById = actor.UserId,
            UpdatedById = actor.UserId,
        };
        _db.ClinicalNoteTemplates.Add(template);
        SetAppointmentTypes(template, request.AppointmentTypeIds);
        AddVersion(_db, template, 1, request.Sections, request.ChangeSummary ?? "First version", actor.UserId);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "template.created", nameof(ClinicalNoteTemplate), template.Id, organization.Id,
            metadata: new { name = template.Name, noteType = template.NoteType.ToString() }, ct: ct);
        return await DetailAsync(template, actor, ct);
    }

    public async Task<DocumentationTemplateDetailDto> UpdateAsync(Guid templateId, SaveDocumentationTemplateRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var template = await LoadOwnedAsync(templateId, organization.Id, ct);
        Validate(request);
        await ValidateAppointmentTypesAsync(request.AppointmentTypeIds, organization.Id, ct);

        template.Name = request.Name.Trim();
        template.NoteType = request.NoteType;
        template.Specialty = request.Specialty;
        template.Description = Trim(request.Description);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.UpdatedById = actor.UserId;
        SetAppointmentTypes(template, request.AppointmentTypeIds);

        var current = await LoadCurrentVersionAsync(template.Id, ct);
        var published = false;
        if (Canonical(ToSectionDtos(current)) != Canonical(Normalize(request.Sections)))
        {
            template.Version = current.VersionNumber + 1;
            AddVersion(_db, template, template.Version, request.Sections, request.ChangeSummary, actor.UserId);
            published = true;
        }
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, published ? "template.version_published" : "template.updated",
            nameof(ClinicalNoteTemplate), template.Id, organization.Id, metadata: new { version = template.Version }, ct: ct);
        return await DetailAsync(template, actor, ct);
    }

    public async Task<DocumentationTemplateDto> SetActiveAsync(Guid templateId, bool isActive, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var template = await LoadOwnedAsync(templateId, organization.Id, ct);
        template.IsActive = isActive;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.UpdatedById = actor.UserId;
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAuditEventAsync(actor.UserId, isActive ? "template.activated" : "template.deactivated",
            nameof(ClinicalNoteTemplate), template.Id, organization.Id, ct: ct);
        return (await ToDtosAsync([template], actor, ct))[0];
    }

    public async Task<DocumentationTemplateDetailDto> CopyAsync(Guid templateId, CopyTemplateRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var source = await LoadVisibleAsync(templateId, actor, ct);
        var current = await LoadCurrentVersionAsync(source.Id, ct);
        var name = string.IsNullOrWhiteSpace(request.Name) ? $"{source.Name} (copy)" : request.Name.Trim();
        return await CreateAsync(new SaveDocumentationTemplateRequest(
            name, source.NoteType, source.Specialty, ToSectionDtos(current), source.Description,
            ChangeSummary: $"Copied from {source.Name} v{current.VersionNumber}"), actor, ct);
    }

    public async Task SetFavoriteAsync(Guid templateId, bool favorite, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var template = await LoadVisibleAsync(templateId, actor, ct);
        var existing = await _db.ProviderFavorites.FirstOrDefaultAsync(f =>
            f.UserId == actor.UserId && f.ItemType == FavoriteItemType.Template && f.ItemId == template.Id, ct);
        if (favorite && existing is null)
            _db.ProviderFavorites.Add(new ProviderFavorite { UserId = actor.UserId, ItemType = FavoriteItemType.Template, ItemId = template.Id });
        else if (!favorite && existing is not null)
            _db.ProviderFavorites.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- note support

    /// <summary>The current version of a template visible to the organization.</summary>
    internal static async Task<Guid?> CurrentVersionIdAsync(PhysioTracDbContext db, Guid organizationId, Guid templateId, CancellationToken ct) =>
        await db.ClinicalNoteTemplateVersions
            .Where(v => v.TemplateId == templateId && (v.Template!.IsSystem || v.Template.OrganizationId == organizationId))
            .OrderByDescending(v => v.VersionNumber).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct);

    /// <summary>The current version of the template a new note should use
    /// (same order as <see cref="SuggestAsync"/>), or null when none fits.</summary>
    internal static async Task<Guid?> SuggestVersionIdAsync(PhysioTracDbContext db, Guid organizationId, Guid userId, NoteType noteType,
        Guid? appointmentTypeId, CancellationToken ct)
    {
        var candidates = await db.ClinicalNoteTemplates
            .Where(t => (t.IsSystem || t.OrganizationId == organizationId) && t.Versions.Any() && t.IsActive && t.NoteType == noteType)
            .Include(t => t.AppointmentTypes).ToListAsync(ct);
        if (candidates.Count == 0) return null;
        var favorites = (await db.ProviderFavorites.Where(f => f.UserId == userId && f.ItemType == FavoriteItemType.Template)
            .Select(f => f.ItemId).ToListAsync(ct)).ToHashSet();
        var best = candidates
            .OrderByDescending(t => appointmentTypeId is Guid at && t.AppointmentTypes.Any(a => a.AppointmentTypeId == at))
            .ThenByDescending(t => favorites.Contains(t.Id))
            .ThenByDescending(t => !t.IsSystem)
            .ThenByDescending(t => t.Specialty == ClinicalSpecialty.General)
            .ThenBy(t => t.Name)
            .First();
        return await CurrentVersionIdAsync(db, organizationId, best.Id, ct);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>System templates plus this organization's own -- only ones
    /// with at least one version (older schema-only rows are not shown).</summary>
    private IQueryable<ClinicalNoteTemplate> Visible(Guid organizationId) =>
        _db.ClinicalNoteTemplates.Where(t => (t.IsSystem || t.OrganizationId == organizationId) && t.Versions.Any());

    private async Task<ClinicalNoteTemplate> LoadVisibleAsync(Guid templateId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        return await Visible(organization.Id).Include(t => t.AppointmentTypes).FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new NotFoundException("Template was not found.");
    }

    private async Task<ClinicalNoteTemplate> LoadOwnedAsync(Guid templateId, Guid organizationId, CancellationToken ct)
    {
        var template = await Visible(organizationId).Include(t => t.AppointmentTypes).FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new NotFoundException("Template was not found.");
        if (template.IsSystem)
            throw new InvalidOperationException("System templates can't be changed. Copy the template to make your own version.");
        return template;
    }

    private async Task<ClinicalNoteTemplateVersion> LoadCurrentVersionAsync(Guid templateId, CancellationToken ct) =>
        await _db.ClinicalNoteTemplateVersions.AsNoTracking().Include(v => v.Sections).Include(v => v.Fields)
            .Where(v => v.TemplateId == templateId).OrderByDescending(v => v.VersionNumber).FirstAsync(ct);

    private static void Validate(SaveDocumentationTemplateRequest request)
    {
        var errors = TemplateRules.ValidateDefinition(request.Name, request.Sections ?? []).ToList();
        if ((request.Description?.Length ?? 0) > 1000) errors.Add("The description is too long (1000 characters at most).");
        if ((request.ChangeSummary?.Length ?? 0) > 500) errors.Add("The change summary is too long (500 characters at most).");
        if (errors.Count > 0) throw new TemplateValidationException(errors);
    }

    private async Task ValidateAppointmentTypesAsync(IReadOnlyList<Guid>? ids, Guid organizationId, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0) return;
        var distinct = ids.Distinct().ToList();
        var found = await _db.AppointmentTypes.CountAsync(a => distinct.Contains(a.Id) && a.OrganizationId == organizationId, ct);
        if (found != distinct.Count) throw new TemplateValidationException(["An appointment type was not found."]);
    }

    private void SetAppointmentTypes(ClinicalNoteTemplate template, IReadOnlyList<Guid>? ids)
    {
        var wanted = (ids ?? []).Distinct().ToHashSet();
        foreach (var link in template.AppointmentTypes.Where(l => !wanted.Contains(l.AppointmentTypeId)).ToList())
        {
            template.AppointmentTypes.Remove(link);
            _db.ClinicalNoteTemplateAppointmentTypes.Remove(link);
        }
        foreach (var id in wanted.Where(id => template.AppointmentTypes.All(l => l.AppointmentTypeId != id)).ToList())
        {
            // Added through the DbSet: a new row with a preset key reached
            // only through a tracked parent's collection would be treated
            // as an existing row to update.
            var link = new ClinicalNoteTemplateAppointmentType { TemplateId = template.Id, AppointmentTypeId = id };
            _db.ClinicalNoteTemplateAppointmentTypes.Add(link);
            template.AppointmentTypes.Add(link);
        }
    }

    /// <summary>Adds a complete, immutable version (sections + fields) for a
    /// template. Shared with the system-template seeder.</summary>
    internal static ClinicalNoteTemplateVersion AddVersion(PhysioTracDbContext db, ClinicalNoteTemplate template, int number,
        IReadOnlyList<TemplateSectionDto> sections, string? changeSummary, Guid? createdById)
    {
        var version = new ClinicalNoteTemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = number,
            ChangeSummary = Trim(changeSummary),
            CreatedById = createdById,
        };
        db.ClinicalNoteTemplateVersions.Add(version);
        foreach (var (s, si) in Normalize(sections).Select((s, i) => (s, i)))
        {
            var section = new ClinicalNoteTemplateSection
            {
                VersionId = version.Id,
                Key = s.Key,
                Title = s.Title.Trim(),
                HelpText = Trim(s.HelpText),
                Component = s.Component,
                DisplayOrder = si,
            };
            db.ClinicalNoteTemplateSections.Add(section);
            foreach (var (f, fi) in s.Fields.Select((f, i) => (f, i)))
            {
                db.ClinicalNoteTemplateFields.Add(new ClinicalNoteTemplateField
                {
                    VersionId = version.Id,
                    SectionId = section.Id,
                    Key = f.Key,
                    Label = f.Label.Trim(),
                    FieldType = f.FieldType,
                    IsRequired = f.IsRequired && f.FieldType != TemplateFieldType.Signature,
                    DisplayOrder = fi,
                    HelpText = Trim(f.HelpText),
                    Placeholder = Trim(f.Placeholder),
                    Unit = Trim(f.Unit),
                    NoteColumn = f.NoteColumn,
                    ConfigJson = ConfigJson(f),
                    ValidationJson = f.Validation is null ? null : JsonSerializer.Serialize(f.Validation, Json),
                    ConditionJson = f.Condition is null ? null : JsonSerializer.Serialize(f.Condition, Json),
                });
            }
        }
        return version;
    }

    private static string? ConfigJson(TemplateFieldDto f)
    {
        var config = new FieldConfig(
            f.Options is { Count: > 0 } ? f.Options.Select(o => o.Trim()).ToList() : null,
            f.Columns is { Count: > 0 } ? f.Columns : null,
            f.FieldType == TemplateFieldType.PainScale ? f.ScaleMin : null,
            f.FieldType == TemplateFieldType.PainScale ? f.ScaleMax : null);
        return config is { Options: null, Columns: null, ScaleMin: null, ScaleMax: null } ? null : JsonSerializer.Serialize(config, Json);
    }

    private sealed record FieldConfig(IReadOnlyList<string>? Options, IReadOnlyList<TableColumnDto>? Columns, decimal? ScaleMin, decimal? ScaleMax);

    /// <summary>Sections/fields in display order with order numbers reset --
    /// what equality and storage both use.</summary>
    internal static IReadOnlyList<TemplateSectionDto> Normalize(IReadOnlyList<TemplateSectionDto> sections) =>
        sections.Select((s, si) => s with
        {
            DisplayOrder = si,
            Fields = s.Fields.Select((f, fi) => f with { Id = null, DisplayOrder = fi }).ToList(),
        }).ToList();

    internal static string Canonical(IReadOnlyList<TemplateSectionDto> sections) =>
        JsonSerializer.Serialize(Normalize(sections).Select(s => new
        {
            s.Key,
            Title = s.Title.Trim(),
            HelpText = Trim(s.HelpText),
            s.Component,
            Fields = s.Fields.Select(f => new
            {
                f.Key,
                Label = f.Label.Trim(),
                f.FieldType,
                f.IsRequired,
                HelpText = Trim(f.HelpText),
                Placeholder = Trim(f.Placeholder),
                Unit = Trim(f.Unit),
                f.NoteColumn,
                ConfigJson = ConfigJson(f),
                Validation = f.Validation is null ? null : JsonSerializer.Serialize(f.Validation, Json),
                Condition = f.Condition is null ? null : JsonSerializer.Serialize(f.Condition, Json),
            }),
        }), Json);

    internal static IReadOnlyList<TemplateSectionDto> ToSectionDtos(ClinicalNoteTemplateVersion version) =>
        version.Sections.OrderBy(s => s.DisplayOrder).Select(s => new TemplateSectionDto(
            s.Key, s.Title,
            version.Fields.Where(f => f.SectionId == s.Id).OrderBy(f => f.DisplayOrder).Select(ToFieldDto).ToList(),
            s.HelpText, s.Component, s.DisplayOrder)).ToList();

    internal static TemplateFieldDto ToFieldDto(ClinicalNoteTemplateField f)
    {
        var config = f.ConfigJson is null ? null : JsonSerializer.Deserialize<FieldConfig>(f.ConfigJson, Json);
        return new TemplateFieldDto(
            f.Key, f.Label, f.FieldType, f.IsRequired, f.HelpText, f.Placeholder, f.Unit, f.NoteColumn,
            config?.Options, config?.Columns, config?.ScaleMin, config?.ScaleMax,
            f.ValidationJson is null ? null : JsonSerializer.Deserialize<FieldValidationDto>(f.ValidationJson, Json),
            f.ConditionJson is null ? null : JsonSerializer.Deserialize<FieldConditionDto>(f.ConditionJson, Json),
            f.Id, f.DisplayOrder);
    }

    private async Task<TemplateVersionDto> ToVersionDtoAsync(ClinicalNoteTemplateVersion v, CancellationToken ct)
    {
        var names = v.CreatedById is Guid by ? await NamesAsync([by], ct) : [];
        return new TemplateVersionDto(v.Id, v.TemplateId, v.VersionNumber, v.ChangeSummary, v.CreatedAt,
            v.CreatedById is Guid c ? names.GetValueOrDefault(c) : null, ToSectionDtos(v));
    }

    private async Task<DocumentationTemplateDetailDto> DetailAsync(ClinicalNoteTemplate template, ICurrentUser actor, CancellationToken ct)
    {
        var current = await LoadCurrentVersionAsync(template.Id, ct);
        return new DocumentationTemplateDetailDto((await ToDtosAsync([template], actor, ct))[0], await ToVersionDtoAsync(current, ct));
    }

    private async Task<IReadOnlyList<DocumentationTemplateDto>> ToDtosAsync(IReadOnlyList<ClinicalNoteTemplate> templates, ICurrentUser actor, CancellationToken ct)
    {
        var ids = templates.Select(t => t.Id).ToList();
        var latest = await _db.ClinicalNoteTemplateVersions.Where(v => ids.Contains(v.TemplateId))
            .GroupBy(v => v.TemplateId)
            .Select(g => g.OrderByDescending(v => v.VersionNumber).Select(v => new { v.TemplateId, v.Id, v.VersionNumber }).First())
            .ToListAsync(ct);
        var favorites = await FavoriteIdsAsync(actor, ct);
        return templates
            .Select(t =>
            {
                var v = latest.First(l => l.TemplateId == t.Id);
                return new DocumentationTemplateDto(t.Id, t.Name, t.NoteType, t.Specialty, t.Description, t.IsSystem, t.IsActive,
                    v.Id, v.VersionNumber, t.AppointmentTypes.Select(a => a.AppointmentTypeId).ToList(), favorites.Contains(t.Id), t.UpdatedAt);
            })
            .OrderByDescending(t => t.IsFavorite).ThenBy(t => t.NoteType).ThenBy(t => t.IsSystem).ThenBy(t => t.Name)
            .ToList();
    }

    private async Task<HashSet<Guid>> FavoriteIdsAsync(ICurrentUser actor, CancellationToken ct) =>
        (await _db.ProviderFavorites.Where(f => f.UserId == actor.UserId && f.ItemType == FavoriteItemType.Template)
            .Select(f => f.ItemId).ToListAsync(ct)).ToHashSet();

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var users = await _db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName }).ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.UserName ?? "");
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

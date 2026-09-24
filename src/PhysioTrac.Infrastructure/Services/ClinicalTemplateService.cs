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

public class ClinicalTemplateService : IClinicalTemplateService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public ClinicalTemplateService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<ClinicalNoteTemplate> CreateAsync(CreateClinicalNoteTemplateRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("A template name is required.");
        }
        try
        {
            using var _ = JsonDocument.Parse(request.SchemaJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("SchemaJson must be valid JSON.");
        }

        Guid? organizationId;
        string? state = null;
        Guid? locationId = null;
        Guid actorOrgId = Guid.Empty;

        if (request.Scope == TemplateScope.Platform)
        {
            _tenantAccess.RequirePlatformSuperAdmin(actor);
            organizationId = null;
            if (request.State is not null || request.LocationId is not null)
            {
                throw new InvalidOperationException("A Platform-scope template cannot specify a state or location.");
            }
        }
        else
        {
            _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
            var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
            actorOrgId = organization.Id;
            organizationId = organization.Id;

            switch (request.Scope)
            {
                case TemplateScope.Organization:
                    if (request.State is not null || request.LocationId is not null)
                    {
                        throw new InvalidOperationException("An Organization-scope template cannot specify a state or location.");
                    }
                    break;
                case TemplateScope.State:
                    if (string.IsNullOrWhiteSpace(request.State) || request.State.Trim().Length != 2)
                    {
                        throw new InvalidOperationException("A State-scope template requires a 2-letter state code.");
                    }
                    if (request.LocationId is not null)
                    {
                        throw new InvalidOperationException("A State-scope template cannot specify a location.");
                    }
                    state = request.State.Trim().ToUpperInvariant();
                    break;
                case TemplateScope.Location:
                    if (request.LocationId is null)
                    {
                        throw new InvalidOperationException("A Location-scope template requires a location.");
                    }
                    if (request.State is not null)
                    {
                        throw new InvalidOperationException("A Location-scope template cannot specify a state.");
                    }
                    var locationExists = await _db.Locations.AnyAsync(l => l.Id == request.LocationId && l.OrganizationId == organization.Id, ct);
                    if (!locationExists)
                    {
                        throw new NotFoundException("Location was not found.");
                    }
                    locationId = request.LocationId;
                    break;
            }
        }

        var existing = await _db.ClinicalNoteTemplates.Where(t =>
                t.OrganizationId == organizationId && t.NoteType == request.NoteType && t.Scope == request.Scope &&
                t.State == state && t.LocationId == locationId && t.IsActive)
            .FirstOrDefaultAsync(ct);

        var nextVersion = 1;
        if (existing is not null)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            nextVersion = existing.Version + 1;
        }

        var template = new ClinicalNoteTemplate
        {
            OrganizationId = organizationId,
            NoteType = request.NoteType,
            Scope = request.Scope,
            State = state,
            LocationId = locationId,
            Name = request.Name.Trim(),
            SchemaJson = request.SchemaJson,
            Version = nextVersion,
            CreatedById = actor.UserId,
        };
        _db.ClinicalNoteTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        if (request.Scope == TemplateScope.Platform)
        {
            await _audit.RecordPlatformAuditEventAsync(actor.UserId, "clinical_template.created", nameof(ClinicalNoteTemplate), template.Id,
                metadata: new { scope = request.Scope.ToString(), version = template.Version }, ct: ct);
        }
        else
        {
            await _audit.RecordAuditEventAsync(actor.UserId, "clinical_template.created", nameof(ClinicalNoteTemplate), template.Id, actorOrgId,
                metadata: new { scope = request.Scope.ToString(), version = template.Version }, ct: ct);
        }

        return template;
    }

    public async Task<IReadOnlyList<ClinicalNoteTemplate>> ListAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        return await _db.ClinicalNoteTemplates
            .Where(t => t.OrganizationId == organization.Id || t.Scope == TemplateScope.Platform)
            .OrderByDescending(t => t.Version)
            .ToListAsync(ct);
    }

    public async Task<ClinicalNoteTemplate?> ResolveAsync(ICurrentUser actor, NoteType noteType, Guid? locationId, string? state, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var normalizedState = string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();

        if (locationId is Guid loc)
        {
            var locationMatch = await _db.ClinicalNoteTemplates.FirstOrDefaultAsync(t =>
                t.IsActive && t.NoteType == noteType && t.Scope == TemplateScope.Location &&
                t.OrganizationId == organization.Id && t.LocationId == loc, ct);
            if (locationMatch is not null) return locationMatch;
        }

        if (normalizedState is not null)
        {
            var stateMatch = await _db.ClinicalNoteTemplates.FirstOrDefaultAsync(t =>
                t.IsActive && t.NoteType == noteType && t.Scope == TemplateScope.State &&
                t.OrganizationId == organization.Id && t.State == normalizedState, ct);
            if (stateMatch is not null) return stateMatch;
        }

        var orgMatch = await _db.ClinicalNoteTemplates.FirstOrDefaultAsync(t =>
            t.IsActive && t.NoteType == noteType && t.Scope == TemplateScope.Organization && t.OrganizationId == organization.Id, ct);
        if (orgMatch is not null) return orgMatch;

        return await _db.ClinicalNoteTemplates.FirstOrDefaultAsync(t =>
            t.IsActive && t.NoteType == noteType && t.Scope == TemplateScope.Platform && t.OrganizationId == null, ct);
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Intake;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Structural mirror of ClinicalTemplateService/ConsentTemplateService
/// for the template-definition half (scoping/versioning/resolution keyed by
/// a string Key instead of an enum), plus the patient-submission half
/// neither of those needs.</summary>
public class IntakeFormService : IIntakeFormService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public IntakeFormService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<IntakeFormTemplate> CreateTemplateAsync(CreateIntakeFormTemplateRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("A form key and name are required.");
        }
        try
        {
            using var _ = JsonDocument.Parse(request.SchemaJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("SchemaJson must be valid JSON.");
        }

        var key = request.Key.Trim().ToLowerInvariant();

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

        var existing = await _db.IntakeFormTemplates.Where(t =>
                t.OrganizationId == organizationId && t.Key == key && t.Scope == request.Scope &&
                t.State == state && t.LocationId == locationId && t.IsActive)
            .FirstOrDefaultAsync(ct);

        var nextVersion = 1;
        if (existing is not null)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            nextVersion = existing.Version + 1;
        }

        var template = new IntakeFormTemplate
        {
            OrganizationId = organizationId,
            Key = key,
            Scope = request.Scope,
            State = state,
            LocationId = locationId,
            Name = request.Name.Trim(),
            SchemaJson = request.SchemaJson,
            Version = nextVersion,
            CreatedById = actor.UserId,
        };
        _db.IntakeFormTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        if (request.Scope == TemplateScope.Platform)
        {
            await _audit.RecordPlatformAuditEventAsync(actor.UserId, "intake_form_template.created", nameof(IntakeFormTemplate), template.Id,
                metadata: new { scope = request.Scope.ToString(), version = template.Version, key }, ct: ct);
        }
        else
        {
            await _audit.RecordAuditEventAsync(actor.UserId, "intake_form_template.created", nameof(IntakeFormTemplate), template.Id, actorOrgId,
                metadata: new { scope = request.Scope.ToString(), version = template.Version, key }, ct: ct);
        }

        return template;
    }

    public async Task<IReadOnlyList<IntakeFormTemplate>> ListTemplatesAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        return await _db.IntakeFormTemplates
            .Where(t => t.OrganizationId == organization.Id || t.Scope == TemplateScope.Platform)
            .OrderBy(t => t.Key).ThenByDescending(t => t.Version)
            .ToListAsync(ct);
    }

    public async Task<IntakeFormTemplate?> ResolveTemplateAsync(ICurrentUser actor, string key, Guid? locationId, string? state, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        return await ResolveInternalAsync(organization.Id, key.Trim().ToLowerInvariant(), locationId, state, ct);
    }

    public async Task<IReadOnlyList<IntakeFormTemplate>> ListResolvedForOrganizationAsync(ICurrentUser actor, Guid? locationId, string? state, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var keys = await _db.IntakeFormTemplates
            .Where(t => t.IsActive && (t.OrganizationId == organization.Id || t.Scope == TemplateScope.Platform))
            .Select(t => t.Key)
            .Distinct()
            .ToListAsync(ct);

        var resolved = new List<IntakeFormTemplate>();
        foreach (var key in keys)
        {
            var template = await ResolveInternalAsync(organization.Id, key, locationId, state, ct);
            if (template is not null) resolved.Add(template);
        }
        return resolved.OrderBy(t => t.Key).ToList();
    }

    public async Task<IntakeFormSubmission> SubmitAsync(ICurrentUser patientUser, SubmitIntakeFormRequest request, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);

        var template = await _db.IntakeFormTemplates.FirstOrDefaultAsync(t => t.Id == request.IntakeFormTemplateId && t.IsActive, ct)
            ?? throw new NotFoundException("Intake form was not found.");
        if (template.Scope != TemplateScope.Platform && template.OrganizationId != patient.OrganizationId)
        {
            throw new NotFoundException("Intake form was not found.");
        }

        try
        {
            using var _ = JsonDocument.Parse(request.ResponseJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("ResponseJson must be valid JSON.");
        }

        var submission = new IntakeFormSubmission
        {
            PatientId = patient.Id,
            IntakeFormTemplateId = template.Id,
            TemplateVersion = template.Version,
            ResponseJson = request.ResponseJson,
        };
        _db.IntakeFormSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(patientUser.UserId, "intake_form.submitted", nameof(IntakeFormSubmission), submission.Id,
            patient.OrganizationId, patientId: patient.Id, metadata: new { key = template.Key, templateVersion = template.Version }, ct: ct);

        return submission;
    }

    public async Task<IReadOnlyList<IntakeFormSubmission>> ListSubmissionsForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.IntakeFormSubmissions
            .Where(s => s.PatientId == patient.Id)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<IntakeFormSubmission> ReviewAsync(Guid submissionId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var submission = await _db.IntakeFormSubmissions.Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct)
            ?? throw new NotFoundException("Submission was not found.");
        if (submission.Patient is null || submission.Patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Submission was not found.");
        }
        if (submission.Status == IntakeFormSubmissionStatus.Reviewed)
        {
            throw new InvalidOperationException("This submission was already reviewed.");
        }

        submission.Status = IntakeFormSubmissionStatus.Reviewed;
        submission.ReviewedById = actor.UserId;
        submission.ReviewedAt = DateTimeOffset.UtcNow;
        submission.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "intake_form.reviewed", nameof(IntakeFormSubmission), submission.Id,
            organization.Id, patientId: submission.PatientId, ct: ct);

        return submission;
    }

    private async Task<IntakeFormTemplate?> ResolveInternalAsync(Guid organizationId, string key, Guid? locationId, string? state, CancellationToken ct)
    {
        var normalizedState = string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();

        if (locationId is Guid loc)
        {
            var locationMatch = await _db.IntakeFormTemplates.FirstOrDefaultAsync(t =>
                t.IsActive && t.Key == key && t.Scope == TemplateScope.Location &&
                t.OrganizationId == organizationId && t.LocationId == loc, ct);
            if (locationMatch is not null) return locationMatch;
        }

        if (normalizedState is not null)
        {
            var stateMatch = await _db.IntakeFormTemplates.FirstOrDefaultAsync(t =>
                t.IsActive && t.Key == key && t.Scope == TemplateScope.State &&
                t.OrganizationId == organizationId && t.State == normalizedState, ct);
            if (stateMatch is not null) return stateMatch;
        }

        var orgMatch = await _db.IntakeFormTemplates.FirstOrDefaultAsync(t =>
            t.IsActive && t.Key == key && t.Scope == TemplateScope.Organization && t.OrganizationId == organizationId, ct);
        if (orgMatch is not null) return orgMatch;

        return await _db.IntakeFormTemplates.FirstOrDefaultAsync(t =>
            t.IsActive && t.Key == key && t.Scope == TemplateScope.Platform && t.OrganizationId == null, ct);
    }
}

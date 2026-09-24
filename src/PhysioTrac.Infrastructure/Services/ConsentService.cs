using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class ConsentService : IConsentService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;
    private readonly IConsentTemplateService _templates;

    public ConsentService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit, IConsentTemplateService templates)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
        _templates = templates;
    }

    public async Task<Consent> RecordAsync(RecordConsentRequest request, ICurrentUser actor, string? ipAddress, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        return await RecordInternalAsync(patient, organization, request.ConsentType, request.SignedByName, actor.UserId, ipAddress, "staff", ct);
    }

    public async Task<Consent> RecordOwnAsync(ICurrentUser patientUser, RecordOwnConsentRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePortalPatientAsync(patientUser, ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(patientUser, ct);

        return await RecordInternalAsync(patient, organization, request.ConsentType, request.SignedByName, patientUser.UserId, ipAddress, "patient_portal", ct);
    }

    /// <summary>Shared by both entry points -- the only difference between
    /// staff recording a consent and a patient signing their own is who's
    /// allowed to call in and how "which patient" gets resolved; the actual
    /// recording (template resolution, snapshotting, audit) is identical.</summary>
    private async Task<Consent> RecordInternalAsync(
        Patient patient, Organization organization, ConsentType consentType, string signedByName,
        Guid actorUserId, string? ipAddress, string source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signedByName))
        {
            throw new InvalidOperationException("A signature name is required.");
        }

        // Resolves the org's current template (most-specific-wins); falls
        // back to the fixed ConsentTypeText constant if nothing has been
        // configured yet -- see ConsentTemplate's own doc comment.
        var template = await _templates.ResolveForOrganizationAsync(organization.Id, consentType, patient.PrimaryLocationId, null, ct);

        var consent = new Consent
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            ConsentType = consentType,
            ConsentText = template?.BodyText ?? ConsentTypeText.For(consentType),
            TemplateVersion = template?.Version,
            SignedByName = signedByName.Trim(),
            RecordedById = actorUserId,
            IpAddress = ipAddress,
        };
        _db.Consents.Add(consent);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actorUserId, "consent.signed", nameof(Consent), consent.Id,
            organization.Id, patientId: patient.Id, ipAddress: ipAddress,
            metadata: new { consentType = consent.ConsentType.ToString(), templateVersion = consent.TemplateVersion, source }, ct: ct);

        return consent;
    }

    public async Task<IReadOnlyList<Consent>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.Consents.Where(c => c.PatientId == patient.Id)
            .OrderByDescending(c => c.SignedAt).ToListAsync(ct);
    }

    public async Task<Consent> RevokeAsync(Guid consentId, RevokeConsentRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var consent = await _db.Consents.FirstOrDefaultAsync(c => c.Id == consentId, ct)
            ?? throw new NotFoundException("Consent was not found.");
        if (consent.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Consent was not found.");
        }
        if (!consent.IsActive)
        {
            throw new InvalidOperationException("This consent was already revoked.");
        }

        consent.RevokedAt = DateTimeOffset.UtcNow;
        consent.RevokedById = actor.UserId;
        consent.RevocationReason = request.Reason;
        consent.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "consent.revoked", nameof(Consent), consent.Id,
            organization.Id, patientId: consent.PatientId,
            metadata: new { consentType = consent.ConsentType.ToString() }, ct: ct);

        return consent;
    }
}

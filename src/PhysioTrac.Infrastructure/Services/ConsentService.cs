using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class ConsentService : IConsentService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public ConsentService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<Consent> RecordAsync(RecordConsentRequest request, ICurrentUser actor, string? ipAddress, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, clinical: false, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (string.IsNullOrWhiteSpace(request.SignedByName))
        {
            throw new InvalidOperationException("A signature name is required.");
        }

        var consent = new Consent
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            ConsentType = request.ConsentType,
            ConsentText = ConsentTypeText.For(request.ConsentType),
            SignedByName = request.SignedByName.Trim(),
            RecordedById = actor.UserId,
            IpAddress = ipAddress,
        };
        _db.Consents.Add(consent);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "consent.signed", nameof(Consent), consent.Id,
            organization.Id, patientId: patient.Id, ipAddress: ipAddress,
            metadata: new { consentType = consent.ConsentType.ToString() }, ct: ct);

        return consent;
    }

    public async Task<IReadOnlyList<Consent>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, clinical: false, ct: ct);
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

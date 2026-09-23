using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Documents;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IFileStorage _fileStorage;
    private readonly IAuditService _audit;
    private readonly StorageOptions _options;

    public DocumentService(
        PhysioTracDbContext db, ITenantAccessService tenantAccess, IFileStorage fileStorage,
        IAuditService audit, IOptions<StorageOptions> options)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _fileStorage = fileStorage;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<PatientDocument> UploadAsync(UploadDocumentRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > _options.MaxUploadBytes)
        {
            throw new InvalidOperationException($"File must be between 1 byte and {_options.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(request.OriginalFilename).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"File type '{extension}' isn't allowed. Allowed types: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        var storageKey = await _fileStorage.SaveAsync(request.Content, ct);

        var document = new PatientDocument
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            UploadedById = actor.UserId,
            Category = request.Category,
            OriginalFilename = request.OriginalFilename,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            Description = request.Description,
            StorageKey = storageKey,
        };
        _db.PatientDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        // Never log the filename or description -- both can contain PHI
        // (e.g. "Jane_Doe_MRI_report.pdf"). Only ids/category, matching this
        // app's no-PHI-in-audit-metadata rule.
        await _audit.RecordAuditEventAsync(actor.UserId, "patient_document.uploaded", nameof(PatientDocument), document.Id,
            organization.Id, patientId: patient.Id, metadata: new { category = document.Category.ToString() }, ct: ct);

        return document;
    }

    public async Task<IReadOnlyList<PatientDocument>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.PatientDocuments
            .Where(d => d.PatientId == patient.Id && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(PatientDocument Document, Stream Content)> DownloadAsync(Guid documentId, ICurrentUser actor, CancellationToken ct = default)
    {
        var document = await LoadDocumentInOrgAsync(documentId, actor, ct);
        await _tenantAccess.RequirePatientAccessAsync(actor, document.PatientId, ct: ct);

        var stream = await _fileStorage.OpenReadAsync(document.StorageKey, ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "patient_document.downloaded", nameof(PatientDocument), document.Id,
            organization.Id, patientId: document.PatientId, ct: ct);

        return (document, stream);
    }

    public async Task DeleteAsync(Guid documentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var document = await LoadDocumentInOrgAsync(documentId, actor, ct);
        await _tenantAccess.RequirePatientAccessAsync(actor, document.PatientId, ct: ct);

        if (document.IsDeleted)
        {
            throw new InvalidOperationException("This document was already deleted.");
        }

        document.DeletedAt = DateTimeOffset.UtcNow;
        document.DeletedById = actor.UserId;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "patient_document.deleted", nameof(PatientDocument), document.Id,
            organization.Id, patientId: document.PatientId, ct: ct);
    }

    private async Task<PatientDocument> LoadDocumentInOrgAsync(Guid documentId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var document = await _db.PatientDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("Document was not found.");
        if (document.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Document was not found.");
        }
        return document;
    }
}

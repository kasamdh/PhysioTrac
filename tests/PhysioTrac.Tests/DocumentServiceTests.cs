using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Documents;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Fake IFileStorage backed by an in-memory dictionary -- keeps
/// these tests fast and isolated from real disk I/O, the same "swap the
/// backend, not the test" reason IFileStorage exists as an interface.</summary>
public class InMemoryFileStorage : IFileStorage
{
    public readonly Dictionary<string, byte[]> Files = new();

    public async Task<string> SaveAsync(Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var key = Guid.NewGuid().ToString("N");
        Files[key] = buffer.ToArray();
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(Files[storageKey]));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

public class DocumentServiceTests
{
    private static (PhysioTracDbContext Db, DocumentService Service, InMemoryFileStorage Storage, Organization Org, Patient Patient, TestCurrentUser FrontDesk)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var storage = new InMemoryFileStorage();
        var options = Options.Create(new StorageOptions());
        var frontDesk = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Scheduler };

        return (db, new DocumentService(db, tenantAccess, storage, audit, options), storage, org, patient, frontDesk);
    }

    private static UploadDocumentRequest ValidRequest(Guid patientId, string filename = "insurance-card.pdf") =>
        new(patientId, DocumentCategory.InsuranceCard, filename, "application/pdf", 11,
            "Front of card", new MemoryStream(Encoding.UTF8.GetBytes("hello world")));

    [Fact]
    public async Task Upload_ValidFile_PersistsMetadataAndBytes()
    {
        var (db, service, storage, _, patient, actor) = NewService();

        var document = await service.UploadAsync(ValidRequest(patient.Id), actor);

        Assert.Single(await db.PatientDocuments.ToListAsync());
        Assert.True(storage.Files.ContainsKey(document.StorageKey));
        Assert.Equal("insurance-card.pdf", document.OriginalFilename);
    }

    [Fact]
    public async Task Upload_DisallowedExtension_Throws()
    {
        var (_, service, _, _, patient, actor) = NewService();
        var request = ValidRequest(patient.Id, filename: "malware.exe");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(request, actor));
    }

    [Fact]
    public async Task Upload_FileTooLarge_Throws()
    {
        var (_, service, _, _, patient, actor) = NewService();
        var request = ValidRequest(patient.Id) with { FileSizeBytes = 100 * 1024 * 1024 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(request, actor));
    }

    [Fact]
    public async Task Upload_PatientRoleCannotUpload()
    {
        var (_, service, _, _, patient, _) = NewService();
        var patientActor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = patient.OrganizationId, Role = UserRole.Patient };

        await Assert.ThrowsAsync<PhysioTrac.Application.Common.ForbiddenException>(
            () => service.UploadAsync(ValidRequest(patient.Id), patientActor));
    }

    [Fact]
    public async Task Download_ReturnsOriginalBytes()
    {
        var (_, service, _, _, patient, actor) = NewService();
        var uploaded = await service.UploadAsync(ValidRequest(patient.Id), actor);

        var (document, content) = await service.DownloadAsync(uploaded.Id, actor);
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync();

        Assert.Equal("hello world", text);
        Assert.Equal(uploaded.Id, document.Id);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndExcludesFromList()
    {
        var (_, service, storage, _, patient, actor) = NewService();
        var uploaded = await service.UploadAsync(ValidRequest(patient.Id), actor);

        await service.DeleteAsync(uploaded.Id, actor);
        var remaining = await service.ListForPatientAsync(patient.Id, actor);

        Assert.Empty(remaining);
        // Soft delete only -- the file itself is never removed from storage.
        Assert.True(storage.Files.ContainsKey(uploaded.StorageKey));
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_Throws()
    {
        var (_, service, _, _, patient, actor) = NewService();
        var uploaded = await service.UploadAsync(ValidRequest(patient.Id), actor);
        await service.DeleteAsync(uploaded.Id, actor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(uploaded.Id, actor));
    }
}

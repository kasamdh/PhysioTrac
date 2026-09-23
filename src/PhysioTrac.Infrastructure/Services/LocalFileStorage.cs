using Microsoft.Extensions.Options;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Documents;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Disk-backed IFileStorage. Files live under
/// StorageOptions.PatientDocumentRoot, named by a fresh Guid -- never the
/// caller-supplied filename -- so nothing here is vulnerable to path
/// traversal via a hostile filename, and the directory is never served as
/// static content by any web server config in this app.</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.IsPathRooted(options.Value.PatientDocumentRoot)
            ? options.Value.PatientDocumentRoot
            : Path.Combine(AppContext.BaseDirectory, options.Value.PatientDocumentRoot);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken ct = default)
    {
        var key = Guid.NewGuid().ToString("N");
        var path = Path.Combine(_root, key);
        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream, ct);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Rejects a storage key that isn't the plain hex Guid this
    /// class itself generates -- defense in depth against a key ever being
    /// influenced by user input on some future call path.</summary>
    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrEmpty(storageKey) || storageKey.Any(c => !Uri.IsHexDigit(c)))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }
        return Path.Combine(_root, storageKey);
    }
}

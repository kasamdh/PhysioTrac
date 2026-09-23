namespace PhysioTrac.Application.Documents;

/// <summary>Storage backend abstraction so PatientDocument's metadata (in
/// SQL) stays decoupled from where the bytes actually live -- local disk
/// today (see Infrastructure's LocalFileStorage), swappable for blob storage
/// later without touching DocumentService or any controller.</summary>
public interface IFileStorage
{
    /// <summary>Persists the stream under a new, opaque storage key (never
    /// derived from the caller-supplied filename) and returns that key.</summary>
    Task<string> SaveAsync(Stream content, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

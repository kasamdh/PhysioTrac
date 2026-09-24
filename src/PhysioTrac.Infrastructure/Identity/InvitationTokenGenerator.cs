using System.Security.Cryptography;
using System.Text;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary>Same token scheme ClientProvisioningService uses for admin
/// invitations (32 random bytes, url-safe base64, SHA-256 hash persisted --
/// never the raw token), factored out so staff invitations (UsersController)
/// can reuse it without duplicating crypto code. Left ClientProvisioningService's
/// own already-tested internal method untouched rather than refactor it to
/// call this, to avoid any risk to that working code.</summary>
public static class InvitationTokenGenerator
{
    public static (string Token, string TokenHash) Generate()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var tokenHash = Hash(token);
        return (token, tokenHash);
    }

    /// <summary>Hashes an already-issued raw token the same way
    /// <see cref="Generate"/> does, for looking up a caller-supplied token
    /// (e.g. a document share link) against the persisted hash.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

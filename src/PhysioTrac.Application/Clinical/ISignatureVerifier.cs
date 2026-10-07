namespace PhysioTrac.Application.Clinical;

/// <summary>Step-up check before an electronic signature: the signer must
/// re-enter their own password, so a signature can't be applied from an
/// unattended, already-signed-in session. Used for signing and cosigning
/// clinical notes.</summary>
public interface ISignatureVerifier
{
    /// <summary>Returns normally when <paramref name="password"/> is the
    /// user's current password; otherwise throws
    /// <see cref="SignatureVerificationException"/>. Wrong attempts count
    /// toward the account's normal sign-in lockout.</summary>
    Task VerifyAsync(Guid userId, string? password, CancellationToken ct = default);
}

/// <summary>The signer's password was missing or wrong, or the account is
/// locked out. Derives from InvalidOperationException so existing callers
/// that surface InvalidOperationException messages show it as-is.</summary>
public class SignatureVerificationException(string message) : InvalidOperationException(message);

using Microsoft.AspNetCore.Identity;
using PhysioTrac.Application.Clinical;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary><see cref="ISignatureVerifier"/> backed by ASP.NET Identity: the
/// same password and the same lockout counter as signing in.</summary>
public class PasswordSignatureVerifier : ISignatureVerifier
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PasswordSignatureVerifier(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task VerifyAsync(Guid userId, string? password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new SignatureVerificationException("Your account could not be found.");

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new SignatureVerificationException(
                "Your account is locked after too many incorrect passwords. Try again later or ask an administrator.");
        }
        if (string.IsNullOrEmpty(password))
        {
            throw new SignatureVerificationException("Enter your password to sign.");
        }
        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            await _userManager.AccessFailedAsync(user);
            throw new SignatureVerificationException("The password is incorrect.");
        }
        await _userManager.ResetAccessFailedCountAsync(user);
    }
}

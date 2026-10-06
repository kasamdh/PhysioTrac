using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using PhysioTrac.Api;
using PhysioTrac.Api.Controllers;

namespace PhysioTrac.Tests;

/// <summary>The strict Auth limiter guards password guessing only; endpoints
/// the SPA hits on every page load must not be under it, or a clinic behind
/// one shared IP gets bounced to the login screen.</summary>
public class AuthRateLimitingTests
{
    private static string? PolicyOn(string action) =>
        typeof(AuthController).GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

    [Theory]
    [InlineData(nameof(AuthController.Login))]
    [InlineData(nameof(AuthController.ChangePassword))]
    [InlineData(nameof(AuthController.GetInvitation))]
    [InlineData(nameof(AuthController.ActivateInvitation))]
    public void PasswordGuessingEndpoints_UseTheStrictAuthPolicy(string action) =>
        Assert.Equal(RateLimitPolicies.Auth, PolicyOn(action));

    [Theory]
    [InlineData(nameof(AuthController.Me))]
    [InlineData(nameof(AuthController.Csrf))]
    [InlineData(nameof(AuthController.Logout))]
    public void PerPageLoadEndpoints_AreNotUnderTheStrictAuthPolicy(string action) =>
        Assert.Null(PolicyOn(action));

    [Fact]
    public void ControllerItself_HasNoBlanketPolicy() =>
        Assert.Null(typeof(AuthController).GetCustomAttribute<EnableRateLimitingAttribute>());
}

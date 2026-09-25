using PhysioTrac.Domain.Entities;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors `PatientInsurance.is_active` — computed from the
/// effective/termination dates, never stored.</summary>
public class PatientInsuranceTests
{
    private static PatientInsurance Policy(DateOnly effective, DateOnly? termination = null) => new()
    {
        EffectiveDate = effective,
        TerminationDate = termination,
        MemberId = "M123",
    };

    [Fact]
    public void ActivePolicy_WithNoTermination_IsActive()
    {
        var policy = Policy(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)));
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void FutureEffectiveDate_IsNotYetActive()
    {
        var policy = Policy(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void PastTerminationDate_IsNoLongerActive()
    {
        var policy = Policy(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void FutureTerminationDate_IsStillActive()
    {
        var policy = Policy(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        Assert.True(policy.IsActive);
    }
}

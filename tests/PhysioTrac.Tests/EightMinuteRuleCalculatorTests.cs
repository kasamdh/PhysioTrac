using PhysioTrac.Application.Billing;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Tests;

public class EightMinuteRuleCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(22, 1)]
    [InlineData(23, 2)]
    [InlineData(37, 2)]
    [InlineData(38, 3)]
    [InlineData(52, 3)]
    [InlineData(53, 4)]
    [InlineData(67, 4)]
    [InlineData(68, 5)]
    public void ComputeUnits_MedicareTable_MatchesTheStandardLookup(int minutes, int expectedUnits)
    {
        Assert.Equal(expectedUnits, EightMinuteRuleCalculator.ComputeUnits(minutes));
        Assert.Equal(expectedUnits, EightMinuteRuleCalculator.ComputeUnits(minutes, EightMinuteRuleVariant.Medicare));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(15, 1)]
    [InlineData(22, 1)]
    [InlineData(23, 2)]
    [InlineData(30, 2)]
    [InlineData(38, 3)]
    [InlineData(45, 3)]
    public void ComputeUnits_RoundedFifteenMinuteVariant_RoundsToNearestQuarterHour(int minutes, int expectedUnits)
    {
        Assert.Equal(expectedUnits, EightMinuteRuleCalculator.ComputeUnits(minutes, EightMinuteRuleVariant.RoundedFifteenMinute));
    }
}

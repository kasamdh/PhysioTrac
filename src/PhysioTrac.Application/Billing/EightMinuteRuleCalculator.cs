namespace PhysioTrac.Application.Billing;

/// <summary>Direct port of `services._medicare_eight_minute_units` —
/// standard CMS 8-minute-rule lookup: total timed minutes -> billable units
/// (8-22=1, 23-37=2, 38-52=3, 53-67=4, +1 per additional 15 minutes). Not
/// every payer follows this exact table — callers must label the result a
/// suggestion, never submit it directly.</summary>
public static class EightMinuteRuleCalculator
{
    public static int ComputeUnits(int totalTimedMinutes)
    {
        if (totalTimedMinutes < 8) return 0;
        return 1 + (totalTimedMinutes - 8) / 15;
    }
}

using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Scheduling;

public static class WeekdayExtensions
{
    /// <summary>Converts a <see cref="DateOnly"/> to our Django-numbered
    /// <see cref="Weekday"/> (Monday=0..Sunday=6), not .NET's own
    /// <see cref="DayOfWeek"/> (Sunday=0).</summary>
    public static Weekday ToWeekday(this DateOnly date) =>
        date.DayOfWeek == DayOfWeek.Sunday ? Weekday.Sunday : (Weekday)((int)date.DayOfWeek - 1);
}

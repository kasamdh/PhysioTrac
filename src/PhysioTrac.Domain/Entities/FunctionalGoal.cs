using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Structured, measurable goal linked directly to a functional limitation.</summary>
public class FunctionalGoal : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid AuthorId { get; set; }

    public string FunctionalLimitation { get; set; } = string.Empty;
    public string FunctionalTask { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MeasurementMethod { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
    public string? SuggestedWording { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Draft;

    public Guid? ApprovedById { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public int? ProgressPercent
    {
        get
        {
            if (CurrentValue is null || TargetValue == BaselineValue) return null;
            var progress = (CurrentValue.Value - BaselineValue) / (TargetValue - BaselineValue) * 100;
            return Math.Max(0, Math.Min(100, (int)Math.Round(progress)));
        }
    }
}

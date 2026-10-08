using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Structured, measurable goal linked directly to a functional limitation.</summary>
public class FunctionalGoal : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>The plan of care the goal belongs to, when set from an evaluation.</summary>
    public Guid? PlanOfCareId { get; set; }
    public PlanOfCare? PlanOfCare { get; set; }

    public Guid AuthorId { get; set; }

    public string FunctionalLimitation { get; set; } = string.Empty;
    public string FunctionalTask { get; set; } = string.Empty;
    public GoalTerm Term { get; set; } = GoalTerm.ShortTerm;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MeasurementMethod { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
    public string? SuggestedWording { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Draft;

    /// <summary>The therapist's standing comments on the goal.</summary>
    public string? Comments { get; set; }

    /// <summary>Goal definition version: bumped by every edit; each version
    /// is kept in <see cref="FunctionalGoalHistory"/>.</summary>
    public int Version { get; set; } = 1;

    public Guid? ApprovedById { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public int? ProgressPercent => FunctionalGoalMath.Progress(BaselineValue, TargetValue, CurrentValue);
}

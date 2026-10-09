using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One append-only entry in a goal's history: its creation,
/// approval, each edit (the full definition as it became) and each progress
/// update (the value and status recorded, and the signed note it came from).
/// Rows are never changed or deleted, so earlier goal versions survive.</summary>
public class FunctionalGoalHistory : BaseEntity
{
    public Guid GoalId { get; set; }
    public FunctionalGoal? Goal { get; set; }

    public GoalHistoryKind Kind { get; set; }
    public int GoalVersion { get; set; }
    public Guid? NoteId { get; set; }
    public Guid RecordedById { get; set; }

    public GoalStatus Status { get; set; }
    public decimal? CurrentValue { get; set; }
    public int? ProgressPercent { get; set; }
    public string? Comment { get; set; }

    /// <summary>The goal's definition at this point (term, wording, baseline,
    /// target, unit, method, target date), as JSON.</summary>
    public string SnapshotJson { get; set; } = "{}";
}

/// <summary>A goal's progress as documented on one note: the goal as it
/// read when the note was written (so a signed note keeps it even if the
/// goal is edited later), this visit's value, status and comment, and
/// whether the line flows into the note's narrative. Applied to the goal
/// itself when the note is signed.</summary>
public class NoteGoalProgress : BaseEntity, INoteOwned
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid GoalId { get; set; }
    public FunctionalGoal? Goal { get; set; }

    public int Order { get; set; }
    public int GoalVersion { get; set; }
    public GoalTerm Term { get; set; }
    public string FunctionalTask { get; set; } = string.Empty;
    public string FunctionalLimitation { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MeasurementMethod { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
    public decimal? PreviousValue { get; set; }

    public decimal? CurrentValue { get; set; }
    public GoalStatus Status { get; set; }
    public string? Comment { get; set; }
    public bool IncludeInNarrative { get; set; } = true;

    public int? ProgressPercent => FunctionalGoalMath.Progress(BaselineValue, TargetValue, CurrentValue);
}

/// <summary>Shared goal arithmetic.</summary>
public static class FunctionalGoalMath
{
    /// <summary>How far the value has moved from baseline toward target,
    /// 0-100 (works for goals that go up or down); null without a value.</summary>
    public static int? Progress(decimal baseline, decimal target, decimal? current)
    {
        if (current is null || target == baseline) return null;
        var progress = (current.Value - baseline) / (target - baseline) * 100;
        return Math.Max(0, Math.Min(100, (int)Math.Round(progress)));
    }
}

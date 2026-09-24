using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record FunctionalGoalDto(
    Guid Id, Guid PatientId, Guid AuthorId, string FunctionalLimitation, string FunctionalTask, GoalTerm Term,
    decimal BaselineValue, decimal TargetValue, decimal? CurrentValue, string Unit, string MeasurementMethod,
    DateOnly TargetDate, GoalStatus Status, int? ProgressPercent, Guid? ApprovedById, DateTimeOffset? ApprovedAt);

public record CreateGoalRequest(
    Guid PatientId, string FunctionalLimitation, string FunctionalTask, GoalTerm Term,
    decimal BaselineValue, decimal TargetValue, string Unit, string MeasurementMethod,
    DateOnly TargetDate, string? SuggestedWording);

public record UpdateGoalProgressRequest(decimal CurrentValue);

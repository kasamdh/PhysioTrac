using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record OutcomeScoreDto(Guid Id, Guid PatientId, Guid? NoteId, Guid RecordedById, OutcomeMeasure Measure, DateOnly MeasuredOn, decimal Score, decimal? MaximumScore);

public record RecordOutcomeScoreRequest(Guid PatientId, Guid? NoteId, OutcomeMeasure Measure, DateOnly MeasuredOn, decimal Score, decimal? MaximumScore, string? Notes);

using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record HomeExerciseItemDto(
    Guid Id, string Name, string? Description, int? Sets, int? Reps,
    int? HoldSeconds, int? FrequencyPerDay, string? Notes, int Order);

public record HomeExerciseProgramDto(
    Guid Id, Guid PatientId, string Title, string? GeneralInstructions,
    HomeExerciseProgramStatus Status, Guid CreatedById, DateTimeOffset CreatedAt,
    IReadOnlyList<HomeExerciseItemDto> Items);

public record CreateHomeExerciseItemRequest(
    string Name, string? Description, int? Sets, int? Reps, int? HoldSeconds, int? FrequencyPerDay, string? Notes);

public record CreateHomeExerciseProgramRequest(
    Guid PatientId, string Title, string? GeneralInstructions, IReadOnlyList<CreateHomeExerciseItemRequest> Items);

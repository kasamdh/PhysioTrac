using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Clinical;

public interface IHomeExerciseProgramService
{
    Task<HomeExerciseProgram> CreateAsync(CreateHomeExerciseProgramRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<HomeExerciseItem> AddItemAsync(Guid programId, CreateHomeExerciseItemRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task RemoveItemAsync(Guid programId, Guid itemId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Marks the program discontinued -- never deleted, so a
    /// patient's exercise history stays intact. Only one active program per
    /// patient is meaningful at a time in the UI, but nothing here enforces
    /// that at the data layer; the clinician discontinues the old one
    /// explicitly when starting a new plan.</summary>
    Task<HomeExerciseProgram> DiscontinueAsync(Guid programId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<HomeExerciseProgram>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);
}

using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>The special-test library: search (favorites first), and for
/// administrators, adding, editing and retiring definitions.</summary>
public interface ISpecialTestLibraryService
{
    Task<IReadOnlyList<SpecialTestDefinitionDto>> SearchAsync(ICurrentUser actor, string? search = null,
        ClinicalSpecialty? specialty = null, bool includeInactive = false, bool favoritesOnly = false, CancellationToken ct = default);

    Task<SpecialTestDefinitionDto> CreateAsync(SaveSpecialTestDefinitionRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>System definitions can only be activated/deactivated, not edited.</summary>
    Task<SpecialTestDefinitionDto> UpdateAsync(Guid id, SaveSpecialTestDefinitionRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<SpecialTestDefinitionDto> SetActiveAsync(Guid id, bool isActive, ICurrentUser actor, CancellationToken ct = default);

    Task SetFavoriteAsync(Guid id, bool favorite, ICurrentUser actor, CancellationToken ct = default);
}

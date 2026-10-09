using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>The approved intervention library, reusable intervention groups,
/// and clinicians' favorites of both.</summary>
public interface IInterventionLibraryService
{
    Task<IReadOnlyList<InterventionLibraryItemDto>> SearchAsync(ICurrentUser actor, string? search = null,
        InterventionCategory? category = null, bool favoritesOnly = false, bool includeInactive = false, CancellationToken ct = default);

    Task<InterventionLibraryItemDto> CreateItemAsync(SaveInterventionLibraryItemRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Built-in items can only be activated/deactivated.</summary>
    Task<InterventionLibraryItemDto> UpdateItemAsync(Guid id, SaveInterventionLibraryItemRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<InterventionLibraryItemDto> SetItemActiveAsync(Guid id, bool isActive, ICurrentUser actor, CancellationToken ct = default);

    Task SetItemFavoriteAsync(Guid id, bool favorite, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The user's own groups and the clinic's shared ones.</summary>
    Task<IReadOnlyList<InterventionGroupDto>> ListGroupsAsync(ICurrentUser actor, CancellationToken ct = default);

    Task<InterventionGroupDto> CreateGroupAsync(SaveInterventionGroupRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<InterventionGroupDto> UpdateGroupAsync(Guid id, SaveInterventionGroupRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Retires a group (kept, no longer listed).</summary>
    Task DeleteGroupAsync(Guid id, ICurrentUser actor, CancellationToken ct = default);

    Task SetGroupFavoriteAsync(Guid id, bool favorite, ICurrentUser actor, CancellationToken ct = default);
}

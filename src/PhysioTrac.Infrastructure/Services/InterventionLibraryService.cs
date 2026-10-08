using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class InterventionLibraryService : IInterventionLibraryService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public InterventionLibraryService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    // ------------------------------------------------------------------ library items

    public async Task<IReadOnlyList<InterventionLibraryItemDto>> SearchAsync(ICurrentUser actor, string? search = null,
        InterventionCategory? category = null, bool favoritesOnly = false, bool includeInactive = false, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var favorites = await FavoritesAsync(actor, FavoriteItemType.Intervention, ct);
        var query = _db.InterventionLibraryItems.AsNoTracking();
        if (!includeInactive) query = query.Where(i => i.IsActive);
        if (category is not null) query = query.Where(i => i.Category == category);
        if (favoritesOnly) query = query.Where(i => favorites.Contains(i.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{SpecialTestLibraryService.EscapeLike(search.Trim())}%";
            query = query.Where(i => EF.Functions.Like(i.Name, like, "\\") ||
                (i.CptCode != null && EF.Functions.Like(i.CptCode, like, "\\")) ||
                (i.BodyRegion != null && EF.Functions.Like(i.BodyRegion, like, "\\")));
        }
        var rows = await query.ToListAsync(ct);
        return rows.Select(i => ToDto(i, favorites.Contains(i.Id)))
            .OrderByDescending(i => i.IsFavorite).ThenBy(i => i.Name).Take(200).ToList();
    }

    public async Task<InterventionLibraryItemDto> CreateItemAsync(SaveInterventionLibraryItemRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        Validate(request);
        var item = new InterventionLibraryItem { Code = await UniqueCodeAsync(request.Name, ct) };
        Apply(item, request);
        _db.InterventionLibraryItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "intervention_library.created", nameof(InterventionLibraryItem), item.Id, item.Name, ct);
        return ToDto(item, false);
    }

    public async Task<InterventionLibraryItemDto> UpdateItemAsync(Guid id, SaveInterventionLibraryItemRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var item = await _db.InterventionLibraryItems.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Intervention was not found.");
        if (item.IsSystem) throw new InvalidOperationException("Built-in interventions can't be edited; deactivate one and add your own instead.");
        Validate(request);
        Apply(item, request);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "intervention_library.updated", nameof(InterventionLibraryItem), item.Id, item.Name, ct);
        return ToDto(item, (await FavoritesAsync(actor, FavoriteItemType.Intervention, ct)).Contains(item.Id));
    }

    public async Task<InterventionLibraryItemDto> SetItemActiveAsync(Guid id, bool isActive, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var item = await _db.InterventionLibraryItems.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Intervention was not found.");
        item.IsActive = isActive;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, isActive ? "intervention_library.activated" : "intervention_library.deactivated",
            nameof(InterventionLibraryItem), item.Id, item.Name, ct);
        return ToDto(item, (await FavoritesAsync(actor, FavoriteItemType.Intervention, ct)).Contains(item.Id));
    }

    public async Task SetItemFavoriteAsync(Guid id, bool favorite, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        if (!await _db.InterventionLibraryItems.AnyAsync(i => i.Id == id, ct)) throw new NotFoundException("Intervention was not found.");
        await SetFavoriteAsync(actor, FavoriteItemType.Intervention, id, favorite, ct);
    }

    // ------------------------------------------------------------------ groups

    public async Task<IReadOnlyList<InterventionGroupDto>> ListGroupsAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var favorites = await FavoritesAsync(actor, FavoriteItemType.InterventionGroup, ct);
        var groups = await _db.InterventionGroups.AsNoTracking().Include(g => g.Items)
            .Where(g => g.IsActive && (g.OwnerUserId == null || g.OwnerUserId == actor.UserId)).ToListAsync(ct);
        return groups.Select(g => ToDto(g, actor, favorites.Contains(g.Id)))
            .OrderByDescending(g => g.IsFavorite).ThenByDescending(g => g.IsMine).ThenBy(g => g.Name).ToList();
    }

    public async Task<InterventionGroupDto> CreateGroupAsync(SaveInterventionGroupRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, request.IsShared ? RoleSets.OrganizationAdministration : RoleSets.Clinical);
        Validate(request);
        var group = new InterventionGroup { OwnerUserId = request.IsShared ? null : actor.UserId };
        _db.InterventionGroups.Add(group);
        ApplyGroup(group, request);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "intervention_group.created", nameof(InterventionGroup), group.Id, group.Name, ct);
        return ToDto(group, actor, false);
    }

    public async Task<InterventionGroupDto> UpdateGroupAsync(Guid id, SaveInterventionGroupRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var group = await LoadEditableGroupAsync(id, actor, ct);
        Validate(request);
        _db.InterventionGroupItems.RemoveRange(group.Items);
        group.Items.Clear();
        ApplyGroup(group, request);
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "intervention_group.updated", nameof(InterventionGroup), group.Id, group.Name, ct);
        return ToDto(group, actor, (await FavoritesAsync(actor, FavoriteItemType.InterventionGroup, ct)).Contains(group.Id));
    }

    public async Task DeleteGroupAsync(Guid id, ICurrentUser actor, CancellationToken ct = default)
    {
        var group = await LoadEditableGroupAsync(id, actor, ct);
        group.IsActive = false;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "intervention_group.retired", nameof(InterventionGroup), group.Id, group.Name, ct);
    }

    public async Task SetGroupFavoriteAsync(Guid id, bool favorite, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        if (!await _db.InterventionGroups.AnyAsync(g => g.Id == id && (g.OwnerUserId == null || g.OwnerUserId == actor.UserId), ct))
            throw new NotFoundException("Group was not found.");
        await SetFavoriteAsync(actor, FavoriteItemType.InterventionGroup, id, favorite, ct);
    }

    /// <summary>Your own group, or (administrators/directors) a shared one.</summary>
    private async Task<InterventionGroup> LoadEditableGroupAsync(Guid id, ICurrentUser actor, CancellationToken ct)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var group = await _db.InterventionGroups.Include(g => g.Items).FirstOrDefaultAsync(g => g.Id == id && g.IsActive, ct)
            ?? throw new NotFoundException("Group was not found.");
        if (group.OwnerUserId is Guid owner)
        {
            if (owner != actor.UserId) throw new NotFoundException("Group was not found.");
        }
        else
        {
            _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        }
        return group;
    }

    private void ApplyGroup(InterventionGroup group, SaveInterventionGroupRequest r)
    {
        group.Name = r.Name.Trim();
        group.Description = Clean(r.Description);
        foreach (var (i, n) in r.Items.Select((i, n) => (i, n)))
        {
            var item = new InterventionGroupItem
            {
                GroupId = group.Id,
                LibraryItemId = i.LibraryItemId,
                Name = i.Name.Trim(),
                Category = i.Category,
                CptCode = Clean(i.CptCode),
                IsTimed = i.IsTimed,
                Sets = i.Sets,
                Repetitions = i.Repetitions,
                Resistance = Clean(i.Resistance),
                Duration = Clean(i.Duration),
                Equipment = Clean(i.Equipment),
                Position = Clean(i.Position),
                Order = n,
            };
            _db.InterventionGroupItems.Add(item);
            // EF adds a tracked parent's new child to its collection itself.
            if (!group.Items.Contains(item)) group.Items.Add(item);
        }
    }

    private static void Validate(SaveInterventionGroupRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name)) errors.Add("Name the group.");
        if ((r.Name?.Length ?? 0) > 120 || (r.Description?.Length ?? 0) > 500) errors.Add("The name or description is too long.");
        if (r.Items.Count == 0) errors.Add("Add at least one intervention to the group.");
        if (r.Items.Count > 40) errors.Add("A group holds 40 interventions at most.");
        if (r.Items.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 300)) errors.Add("Every intervention in the group needs a name (300 characters at most).");
        if (errors.Count > 0) throw new TemplateValidationException(errors);
    }

    private static void Validate(SaveInterventionLibraryItemRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name)) errors.Add("Name the intervention.");
        if ((r.Name?.Length ?? 0) > 150 || (r.CptCode?.Length ?? 0) > 10 || (r.Description?.Length ?? 0) > 1000)
            errors.Add("A value is too long.");
        if (r.CptCode is { Length: > 0 } cpt && !System.Text.RegularExpressions.Regex.IsMatch(cpt.Trim(), "^[A-Z0-9]{4,5}$"))
            errors.Add("A CPT/HCPCS code is 4–5 letters or digits (e.g. 97110).");
        if (errors.Count > 0) throw new TemplateValidationException(errors);
    }

    private static void Apply(InterventionLibraryItem i, SaveInterventionLibraryItemRequest r)
    {
        i.Name = r.Name.Trim();
        i.Category = r.Category;
        i.IsTimed = r.IsTimed;
        i.CptCode = Clean(r.CptCode)?.ToUpperInvariant();
        i.BodyRegion = Clean(r.BodyRegion);
        i.Description = Clean(r.Description);
        i.DefaultSets = r.DefaultSets;
        i.DefaultRepetitions = r.DefaultRepetitions;
        i.DefaultResistance = Clean(r.DefaultResistance);
        i.DefaultDuration = Clean(r.DefaultDuration);
        i.DefaultEquipment = Clean(r.DefaultEquipment);
        i.DefaultPosition = Clean(r.DefaultPosition);
    }

    private async Task<string> UniqueCodeAsync(string name, CancellationToken ct)
    {
        var slug = new string(name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        var baseCode = "clinic-" + (slug.Length > 50 ? slug[..50] : slug);
        var code = baseCode;
        for (var n = 2; await _db.InterventionLibraryItems.AnyAsync(i => i.Code == code, ct); n++) code = $"{baseCode}-{n}";
        return code;
    }

    private async Task<HashSet<Guid>> FavoritesAsync(ICurrentUser actor, FavoriteItemType type, CancellationToken ct) =>
        (await _db.ProviderFavorites.Where(f => f.UserId == actor.UserId && f.ItemType == type).Select(f => f.ItemId).ToListAsync(ct)).ToHashSet();

    private async Task SetFavoriteAsync(ICurrentUser actor, FavoriteItemType type, Guid id, bool favorite, CancellationToken ct)
    {
        var existing = await _db.ProviderFavorites.FirstOrDefaultAsync(f => f.UserId == actor.UserId && f.ItemType == type && f.ItemId == id, ct);
        if (favorite && existing is null) _db.ProviderFavorites.Add(new ProviderFavorite { UserId = actor.UserId, ItemType = type, ItemId = id });
        else if (!favorite && existing is not null) _db.ProviderFavorites.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    private async Task AuditAsync(ICurrentUser actor, string action, string type, Guid id, string name, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, action, type, id, organization.Id, metadata: new { name }, ct: ct);
    }

    internal static InterventionLibraryItemDto ToDto(InterventionLibraryItem i, bool favorite) => new(
        i.Id, i.Code, i.Name, i.Category, i.CptCode, i.IsTimed, i.BodyRegion, i.Description, i.DefaultSets, i.DefaultRepetitions,
        i.DefaultResistance, i.DefaultDuration, i.DefaultEquipment, i.DefaultPosition, i.IsActive, i.IsSystem, favorite);

    private static InterventionGroupDto ToDto(InterventionGroup g, ICurrentUser actor, bool favorite) => new(
        g.Id, g.Name, g.Description, g.OwnerUserId is null, g.OwnerUserId == actor.UserId, favorite,
        g.Items.OrderBy(i => i.Order).Select(i => new InterventionGroupItemDto(i.Name, i.Category, i.IsTimed, i.CptCode, i.Sets,
            i.Repetitions, i.Resistance, i.Duration, i.Equipment, i.Position, i.LibraryItemId)).ToList());

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

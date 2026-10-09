using System.Text.RegularExpressions;
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

public partial class SpecialTestLibraryService : ISpecialTestLibraryService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public SpecialTestLibraryService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SpecialTestDefinitionDto>> SearchAsync(ICurrentUser actor, string? search = null,
        ClinicalSpecialty? specialty = null, bool includeInactive = false, bool favoritesOnly = false, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var favorites = await FavoritesAsync(actor, ct);
        var query = _db.SpecialTestDefinitions.AsNoTracking();
        if (!includeInactive) query = query.Where(d => d.IsActive);
        if (specialty is not null) query = query.Where(d => d.Specialty == specialty);
        if (favoritesOnly) query = query.Where(d => favorites.Contains(d.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(d => EF.Functions.Like(d.Name, like, "\\") ||
                (d.BodyRegion != null && EF.Functions.Like(d.BodyRegion, like, "\\")) ||
                (d.Description != null && EF.Functions.Like(d.Description, like, "\\")));
        }
        var rows = await query.ToListAsync(ct);
        return rows.Select(d => ToDto(d, favorites.Contains(d.Id)))
            .OrderByDescending(d => d.IsFavorite).ThenBy(d => d.Name).Take(200).ToList();
    }

    public async Task<SpecialTestDefinitionDto> CreateAsync(SaveSpecialTestDefinitionRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        Validate(request);
        var code = await UniqueCodeAsync(request.Name, ct);
        var definition = new SpecialTestDefinition { Code = code };
        Apply(definition, request);
        _db.SpecialTestDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "special_test.created", definition, ct);
        return ToDto(definition, false);
    }

    public async Task<SpecialTestDefinitionDto> UpdateAsync(Guid id, SaveSpecialTestDefinitionRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var definition = await _db.SpecialTestDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Special test was not found.");
        if (definition.IsSystem) throw new InvalidOperationException("Built-in special tests can't be edited; deactivate one and add your own instead.");
        Validate(request);
        Apply(definition, request);
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, "special_test.updated", definition, ct);
        return ToDto(definition, (await FavoritesAsync(actor, ct)).Contains(definition.Id));
    }

    public async Task<SpecialTestDefinitionDto> SetActiveAsync(Guid id, bool isActive, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var definition = await _db.SpecialTestDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Special test was not found.");
        definition.IsActive = isActive;
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(actor, isActive ? "special_test.activated" : "special_test.deactivated", definition, ct);
        return ToDto(definition, (await FavoritesAsync(actor, ct)).Contains(definition.Id));
    }

    public async Task SetFavoriteAsync(Guid id, bool favorite, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        if (!await _db.SpecialTestDefinitions.AnyAsync(d => d.Id == id, ct)) throw new NotFoundException("Special test was not found.");
        var existing = await _db.ProviderFavorites.FirstOrDefaultAsync(f =>
            f.UserId == actor.UserId && f.ItemType == FavoriteItemType.SpecialTest && f.ItemId == id, ct);
        if (favorite && existing is null)
            _db.ProviderFavorites.Add(new ProviderFavorite { UserId = actor.UserId, ItemType = FavoriteItemType.SpecialTest, ItemId = id });
        else if (!favorite && existing is not null)
            _db.ProviderFavorites.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(SaveSpecialTestDefinitionRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name)) errors.Add("Enter the test name.");
        if ((r.Name?.Length ?? 0) > 150 || (r.BodyRegion?.Length ?? 0) > 60 || (r.Unit?.Length ?? 0) > 20 ||
            (r.Description?.Length ?? 0) > 1000 || (r.InterpretationGuide?.Length ?? 0) > 1000 || (r.ContraindicationWarning?.Length ?? 0) > 1000)
            errors.Add("A value is too long.");
        if (r.ResultKind != SpecialTestResultKind.PositiveNegative && string.IsNullOrWhiteSpace(r.Unit))
            errors.Add("A test with a numeric result needs its unit.");
        if (errors.Count > 0) throw new TemplateValidationException(errors);
    }

    private static void Apply(SpecialTestDefinition d, SaveSpecialTestDefinitionRequest r)
    {
        d.Name = r.Name.Trim();
        d.Specialty = r.Specialty;
        d.ResultKind = r.ResultKind;
        d.BodyRegion = Clean(r.BodyRegion);
        d.Description = Clean(r.Description);
        d.Unit = Clean(r.Unit);
        d.InterpretationGuide = Clean(r.InterpretationGuide);
        d.ContraindicationWarning = Clean(r.ContraindicationWarning);
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();

    private async Task<string> UniqueCodeAsync(string name, CancellationToken ct)
    {
        var baseCode = "clinic-" + NonWord().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        baseCode = baseCode.Length > 50 ? baseCode[..50] : baseCode;
        var code = baseCode;
        for (var n = 2; await _db.SpecialTestDefinitions.AnyAsync(d => d.Code == code, ct); n++) code = $"{baseCode}-{n}";
        return code;
    }

    private async Task<HashSet<Guid>> FavoritesAsync(ICurrentUser actor, CancellationToken ct) =>
        (await _db.ProviderFavorites.Where(f => f.UserId == actor.UserId && f.ItemType == FavoriteItemType.SpecialTest)
            .Select(f => f.ItemId).ToListAsync(ct)).ToHashSet();

    private async Task AuditAsync(ICurrentUser actor, string action, SpecialTestDefinition d, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, action, nameof(SpecialTestDefinition), d.Id, organization.Id,
            metadata: new { name = d.Name }, ct: ct);
    }

    internal static SpecialTestDefinitionDto ToDto(SpecialTestDefinition d, bool favorite) => new(
        d.Id, d.Code, d.Name, d.Specialty, d.BodyRegion, d.Description, d.ResultKind, d.Unit, d.InterpretationGuide,
        d.ContraindicationWarning, d.IsActive, d.IsSystem, favorite);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Case-insensitive "contains" on every database: LIKE with the
    /// search text's own wildcards escaped.</summary>
    internal static string EscapeLike(string s) => s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");
}

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.SuperAdmin;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `care/client_management.py`.</summary>
public class ClientProvisioningService : IClientProvisioningService
{
    private readonly PhysioTracDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly AppOptions _appOptions;

    public ClientProvisioningService(
        PhysioTracDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit, IOptions<AppOptions> appOptions)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _appOptions = appOptions.Value;
    }

    public async Task<ProvisionedClientResult> ProvisionClientAsync(ProvisionClientRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var adminEmail = request.AdminEmail.Trim().ToLowerInvariant();
        if (await _userManager.FindByNameAsync(adminEmail) is not null)
        {
            throw new InvalidOperationException("An account with the administrator email already exists.");
        }

        // The InMemory provider (used by tests) doesn't support transactions
        // at all — only wrap in one against a real relational provider.
        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;

        var slug = await UniqueSlugAsync(request.ClientName, ct);
        var clientNumber = await NextClientNumberAsync(ct);

        var organization = new Organization
        {
            ClientNumber = clientNumber,
            Name = request.ClientName.Trim(),
            Slug = slug,
            PortalUrl = $"{_appOptions.FrontendBaseUrl}/{slug}",
            SupportEmail = request.ClientEmail.Trim().ToLowerInvariant(),
            SupportPhone = request.ClientPhone?.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = request.AddressLine2?.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            ZipCode = request.ZipCode.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "United States" : request.Country.Trim(),
            SubscriptionTier = request.SubscriptionTier,
            Timezone = request.Timezone,
            Comments = request.Comments?.Trim(),
            CreatedById = actor.UserId,
            UpdatedById = actor.UserId,
        };
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(ct);

        var administrator = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = request.AdminFirstName.Trim(),
            LastName = request.AdminLastName.Trim(),
            OrganizationId = organization.Id,
            Role = UserRole.Admin,
        };
        var createResult = await _userManager.CreateAsync(administrator); // no password: mirrors set_unusable_password()
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        var (token, activationUrl) = await IssueInvitationInternalAsync(organization, administrator.Id, ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "client.created", nameof(Organization), organization.Id, organization.Id,
            metadata: new { clientNumber = organization.ClientNumber }, ct: ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "client_admin.created", nameof(ApplicationUser), administrator.Id, organization.Id,
            metadata: new { clientNumber = organization.ClientNumber }, ct: ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
            await transaction.DisposeAsync();
        }

        return new ProvisionedClientResult(await ToDtoAsync(organization, ct), administrator.Id, adminEmail, activationUrl, token);
    }

    public async Task<ClientDto> UpdateClientAsync(long clientNumber, UpdateClientRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await RequireOrganizationAsync(clientNumber, ct);
        var changedFields = new List<string>();

        if (request.ClientName is not null) { organization.Name = request.ClientName.Trim(); changedFields.Add("name"); }
        if (request.ClientEmail is not null) { organization.SupportEmail = request.ClientEmail.Trim(); changedFields.Add("support_email"); }
        if (request.ClientPhone is not null) { organization.SupportPhone = request.ClientPhone.Trim(); changedFields.Add("support_phone"); }
        if (request.AddressLine1 is not null) { organization.AddressLine1 = request.AddressLine1.Trim(); changedFields.Add("address_line_1"); }
        if (request.AddressLine2 is not null) { organization.AddressLine2 = request.AddressLine2.Trim(); changedFields.Add("address_line_2"); }
        if (request.City is not null) { organization.City = request.City.Trim(); changedFields.Add("city"); }
        if (request.State is not null) { organization.State = request.State.Trim(); changedFields.Add("state"); }
        if (request.ZipCode is not null) { organization.ZipCode = request.ZipCode.Trim(); changedFields.Add("zip_code"); }
        if (request.Country is not null) { organization.Country = request.Country.Trim(); changedFields.Add("country"); }
        if (request.SubscriptionTier is not null) { organization.SubscriptionTier = request.SubscriptionTier.Value; changedFields.Add("subscription_tier"); }
        if (request.Timezone is not null) { organization.Timezone = request.Timezone; changedFields.Add("timezone"); }
        if (request.Comments is not null) { organization.Comments = request.Comments.Trim(); changedFields.Add("comments"); }

        organization.UpdatedById = actor.UserId;
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (changedFields.Count > 0)
        {
            await _audit.RecordAuditEventAsync(actor.UserId, "client.updated", nameof(Organization), organization.Id, organization.Id,
                metadata: new { clientNumber = organization.ClientNumber, changedFields }, ct: ct);
        }

        return await ToDtoAsync(organization, ct);
    }

    public async Task<ClientDto> SuspendClientAsync(long clientNumber, string reason, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await RequireOrganizationAsync(clientNumber, ct);
        if (organization.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Client is already suspended.");
        }
        organization.Status = OrganizationStatus.Suspended;
        organization.IsActive = false;
        organization.SuspendedAt = DateTimeOffset.UtcNow;
        organization.SuspendedById = actor.UserId;
        organization.SuspensionReason = reason.Trim();
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "client.suspended", nameof(Organization), organization.Id, organization.Id,
            metadata: new { clientNumber = organization.ClientNumber }, ct: ct);
        return await ToDtoAsync(organization, ct);
    }

    public async Task<ClientDto> ActivateClientAsync(long clientNumber, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await RequireOrganizationAsync(clientNumber, ct);
        if (organization.Status == OrganizationStatus.Active)
        {
            throw new InvalidOperationException("Client is already active.");
        }
        organization.Status = OrganizationStatus.Active;
        organization.IsActive = true;
        organization.SuspendedAt = null;
        organization.SuspendedById = null;
        organization.SuspensionReason = null;
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "client.reactivated", nameof(Organization), organization.Id, organization.Id,
            metadata: new { clientNumber = organization.ClientNumber }, ct: ct);
        return await ToDtoAsync(organization, ct);
    }

    public async Task<ClientDto> ArchiveClientAsync(long clientNumber, string? reason, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await RequireOrganizationAsync(clientNumber, ct);
        if (organization.ArchivedAt is not null)
        {
            throw new InvalidOperationException("Client is already archived.");
        }
        organization.IsActive = false;
        organization.ArchivedAt = DateTimeOffset.UtcNow;
        organization.ArchivedById = actor.UserId;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            organization.SuspensionReason = reason.Trim();
        }
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "client.archived", nameof(Organization), organization.Id, organization.Id,
            metadata: new { clientNumber = organization.ClientNumber }, ct: ct);
        return await ToDtoAsync(organization, ct);
    }

    public async Task<ClientDto?> GetClientAsync(long clientNumber, CancellationToken ct = default)
    {
        var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.ClientNumber == clientNumber, ct);
        return organization is null ? null : await ToDtoAsync(organization, ct);
    }

    public async Task<PagedResult<ClientDto>> ListClientsAsync(ClientListQuery query, CancellationToken ct = default)
    {
        var q = _db.Organizations.AsQueryable();

        if (!query.IncludeArchived && query.Status is null)
        {
            q = q.Where(o => o.ArchivedAt == null);
        }
        if (query.Status is not null)
        {
            q = q.Where(o => o.Status == query.Status);
        }
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var text = query.Query.Trim();
            q = q.Where(o => o.Name.Contains(text) || (o.SupportEmail ?? "").Contains(text) || (o.City ?? "").Contains(text));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var records = await q.OrderBy(o => o.ClientNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var dtos = new List<ClientDto>();
        foreach (var org in records) dtos.Add(await ToDtoAsync(org, ct));

        return new PagedResult<ClientDto>(dtos, total, page, pageSize);
    }

    public async Task<(string Token, string ActivationUrl)> ResendAdminInvitationAsync(long clientNumber, CancellationToken ct = default)
    {
        var organization = await RequireOrganizationAsync(clientNumber, ct);
        var administrator = await _db.Users
            .Where(u => u.OrganizationId == organization.Id && u.Role == UserRole.Admin)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("This client has no administrator account.");

        return await IssueInvitationInternalAsync(organization, administrator.Id, ct);
    }

    private async Task<(string Token, string ActivationUrl)> IssueInvitationInternalAsync(Organization organization, Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var stale = await _db.ClientInvitations.Where(i => i.UserId == userId && i.UsedAt == null).ToListAsync(ct);
        foreach (var invite in stale) invite.UsedAt = now;

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        _db.ClientInvitations.Add(new ClientInvitation
        {
            OrganizationId = organization.Id,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = now.AddDays(7),
        });
        await _db.SaveChangesAsync(ct);

        var activationUrl = $"{_appOptions.FrontendBaseUrl}/{organization.Slug}/activate?token={token}";
        return (token, activationUrl);
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slug.Slugify(name);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "client";
        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Organizations.AnyAsync(o => o.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }
        return slug;
    }

    /// <summary>Locks the single-row sequence table (matching the original's
    /// `select_for_update()`) so two concurrent provisions can never be
    /// handed the same client number. Row-locking hints are relational-only;
    /// the InMemory provider (tests) falls back to a plain read-increment-
    /// write, which is fine for a single-threaded test but is NOT the code
    /// path that runs in production.</summary>
    private async Task<long> NextClientNumberAsync(CancellationToken ct)
    {
        long next;
        ClientNumberSequence? trackedSequence = null;

        if (_db.Database.IsRealSqlServer())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "IF NOT EXISTS (SELECT 1 FROM ClientNumberSequences WHERE Id = 1) " +
                "INSERT INTO ClientNumberSequences (Id, NextNumber) VALUES (1, 1000)", ct);

            var rows = await _db.Database.SqlQueryRaw<long>(
                "SELECT NextNumber FROM ClientNumberSequences WITH (UPDLOCK, ROWLOCK) WHERE Id = 1").ToListAsync(ct);
            next = rows.First();
        }
        else
        {
            trackedSequence = await _db.ClientNumberSequences.FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (trackedSequence is null)
            {
                trackedSequence = new ClientNumberSequence { Id = 1, NextNumber = 1000 };
                _db.ClientNumberSequences.Add(trackedSequence);
                await _db.SaveChangesAsync(ct);
            }
            next = trackedSequence.NextNumber;
        }

        var existingMax = await _db.Organizations.Where(o => o.ClientNumber != null)
            .Select(o => o.ClientNumber!.Value).OrderByDescending(n => n).FirstOrDefaultAsync(ct);
        var number = Math.Max(next, Math.Max(existingMax, 999) + 1);

        if (_db.Database.IsRealSqlServer())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE ClientNumberSequences SET NextNumber = {0} WHERE Id = 1", new object[] { number + 1 }, ct);
        }
        else
        {
            // Must match the branch NextClientNumberAsync took above —
            // trackedSequence is only populated there for a non-SqlServer provider.
            trackedSequence!.NextNumber = number + 1;
            await _db.SaveChangesAsync(ct);
        }

        return number;
    }

    private async Task<Organization> RequireOrganizationAsync(long clientNumber, CancellationToken ct)
    {
        return await _db.Organizations.FirstOrDefaultAsync(o => o.ClientNumber == clientNumber, ct)
            ?? throw new NotFoundException("Client was not found.");
    }

    private async Task<ClientDto> ToDtoAsync(Organization org, CancellationToken ct)
    {
        var administrator = await _db.Users
            .Where(u => u.OrganizationId == org.Id && u.Role == UserRole.Admin)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .FirstOrDefaultAsync(ct);
        var userCount = await _db.Users.CountAsync(u => u.OrganizationId == org.Id, ct);

        return new ClientDto(
            org.Id, org.ClientNumber, org.Name, org.Slug, org.PortalUrl, org.SupportEmail, org.SupportPhone,
            org.City, org.State, org.AddressLine1, org.AddressLine2, org.ZipCode, org.Country,
            org.SubscriptionTier, org.Timezone, org.Status, org.Comments, userCount,
            administrator is null ? null : new ClientAdminSummary(administrator.Id, $"{administrator.FirstName} {administrator.LastName}".Trim(), administrator.Email ?? string.Empty),
            org.CreatedAt, org.UpdatedAt, org.SuspendedAt, org.ArchivedAt);
    }
}

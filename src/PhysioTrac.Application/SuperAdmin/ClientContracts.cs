using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.SuperAdmin;

public record ClientAdminSummary(Guid Id, string Name, string Email);

public record ClientDto(
    Guid Id,
    long? ClientNumber,
    string ClientName,
    string Slug,
    string? PortalUrl,
    string? Email,
    string? Phone,
    string? City,
    string? State,
    string? AddressLine1,
    string? AddressLine2,
    string? ZipCode,
    string Country,
    SubscriptionTier SubscriptionTier,
    string Timezone,
    OrganizationStatus Status,
    string? Comments,
    int UserCount,
    int LocationCount,
    int PatientCount,
    long StorageBytesUsed,
    DateOnly? TrialEndDate,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    ClientAdminSummary? PrimaryAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SuspendedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ArchivedAt);

public record ProvisionClientRequest(
    string ClientName,
    string ClientEmail,
    string? ClientPhone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string ZipCode,
    string? Country,
    SubscriptionTier SubscriptionTier,
    string Timezone,
    string? Comments,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string LocationName);

public record UpdateClientRequest(
    string? ClientName,
    string? ClientEmail,
    string? ClientPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? ZipCode,
    string? Country,
    SubscriptionTier? SubscriptionTier,
    string? Timezone,
    string? Comments,
    DateOnly? TrialEndDate,
    string? StripeCustomerId,
    string? StripeSubscriptionId);

public record ProvisionedClientResult(ClientDto Client, Guid AdministratorId, string AdministratorEmail, string InvitationUrl, string DevelopmentInviteToken);

public record ClientListQuery(string? Query, OrganizationStatus? Status, bool IncludeArchived, int Page = 1, int PageSize = 25);

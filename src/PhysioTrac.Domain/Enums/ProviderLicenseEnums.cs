namespace PhysioTrac.Domain.Enums;

public enum ProviderLicenseStatus
{
    Active,
    Pending,
    Expired,
    Suspended,
    Revoked,
}

/// <summary>Tiered expiration warning for a license, most urgent last.
/// Ordered so a caller can e.g. sort a report by descending severity.</summary>
public enum LicenseExpirationAlertLevel
{
    None,
    Notice90,
    Notice60,
    Notice30,
    Expired,
}

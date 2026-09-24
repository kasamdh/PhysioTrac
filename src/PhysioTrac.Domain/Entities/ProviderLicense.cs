using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One PT/PTA license or compact privilege held by a provider in a
/// specific U.S. state. A provider can hold more than one row -- a home-
/// state full license plus one or more PT Compact privilege rows for other
/// member states -- which is exactly why this is a child collection on
/// Provider rather than the flat LicenseNumber field it replaces. No direct
/// OrganizationId here: tenant scoping always flows through
/// ProviderId -> Provider.OrganizationId, the same pattern
/// NoteIntervention/HomeExerciseItem already use for a note/program's own
/// child rows.
///
/// Deliberately simplified vs. the real PT Compact: a compact-privilege row
/// doesn't link back to the specific home-state license it derives from --
/// that relationship (and eligibility rules) is real Compact Commission
/// business logic not modeled here yet.</summary>
public class ProviderLicense : BaseEntity
{
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }

    /// <summary>Two-letter USPS state code (e.g. "NC"), not a free-text state name.</summary>
    public string State { get; set; } = string.Empty;

    public string LicenseNumber { get; set; } = string.Empty;
    public DateOnly? IssueDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public ProviderLicenseStatus Status { get; set; } = ProviderLicenseStatus.Active;

    /// <summary>True for a PT Compact privilege to practice in this state;
    /// false for a full, independently-issued state license.</summary>
    public bool IsCompactPrivilege { get; set; }

    public string? Notes { get; set; }

    public bool IsExpired => ExpirationDate < DateOnly.FromDateTime(DateTime.UtcNow);

    public int DaysUntilExpiration => ExpirationDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;

    /// <summary>Within 90 days of expiring, and not already expired/inactive --
    /// a fixed threshold today, not yet a per-organization/per-state
    /// configurable policy (the spec's "configurable templates and policies
    /// per organization/location/state" is a later module).</summary>
    public bool IsExpiringSoon => Status == ProviderLicenseStatus.Active && !IsExpired && DaysUntilExpiration <= 90;

    /// <summary>Tiered version of <see cref="IsExpiringSoon"/> for a report/
    /// alert list -- fixed 90/60/30-day thresholds today, same caveat as
    /// above about per-organization configurability being a later module.
    /// An inactive (Suspended/Revoked/Pending) license never alerts: there's
    /// nothing actionable about a license that isn't currently relied on.</summary>
    public LicenseExpirationAlertLevel ExpirationAlertLevel
    {
        get
        {
            if (Status != ProviderLicenseStatus.Active) return LicenseExpirationAlertLevel.None;
            if (IsExpired) return LicenseExpirationAlertLevel.Expired;
            if (DaysUntilExpiration <= 30) return LicenseExpirationAlertLevel.Notice30;
            if (DaysUntilExpiration <= 60) return LicenseExpirationAlertLevel.Notice60;
            if (DaysUntilExpiration <= 90) return LicenseExpirationAlertLevel.Notice90;
            return LicenseExpirationAlertLevel.None;
        }
    }
}

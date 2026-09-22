using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One insurance policy on a patient's chart. Multiple rows
/// accumulate over time (terminated policies are kept, never deleted, for
/// billing history) — <see cref="IsActive"/> is computed from the effective/
/// termination dates, never stored.
///
/// Deliberately omits the original's `card_front`/`card_back` image fields —
/// private file storage isn't ported yet.</summary>
public class PatientInsurance : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid PayerId { get; set; }
    public Payer? Payer { get; set; }

    public InsuranceRank Rank { get; set; } = InsuranceRank.Primary;
    public string? PlanName { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string? GroupNumber { get; set; }
    public string? SubscriberName { get; set; }
    public DateOnly? SubscriberDateOfBirth { get; set; }
    public RelationshipToSubscriber RelationshipToSubscriber { get; set; } = RelationshipToSubscriber.Self;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public decimal? Copay { get; set; }
    public decimal? CoinsurancePercent { get; set; }
    public decimal? Deductible { get; set; }
    public bool AuthorizationRequired { get; set; }
    public Guid? CreatedById { get; set; }

    public bool IsActive
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (EffectiveDate > today) return false;
            if (TerminationDate is DateOnly termination && termination <= today) return false;
            return true;
        }
    }
}

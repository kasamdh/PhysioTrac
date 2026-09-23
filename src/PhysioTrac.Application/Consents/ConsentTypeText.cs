using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Consents;

/// <summary>Fixed consent language per type -- placeholder legal text, not
/// reviewed by counsel. A real clinic-configurable template system
/// (mirroring NoteTemplates for clinical documentation) is a later module;
/// today every clinic sees the same wording for a given ConsentType.</summary>
public static class ConsentTypeText
{
    public static string For(ConsentType type) => type switch
    {
        ConsentType.HipaaAcknowledgment =>
            "I acknowledge that I have been offered a copy of this clinic's Notice of Privacy Practices, " +
            "describing how my protected health information may be used and disclosed.",
        ConsentType.FinancialPolicy =>
            "I understand and agree to this clinic's financial policy, including my responsibility for " +
            "copays, deductibles, coinsurance, and any balance not covered by insurance.",
        ConsentType.ConsentToTreat =>
            "I voluntarily consent to physical therapy evaluation and treatment as recommended by my " +
            "treating clinician, and understand that no guarantee of outcome has been made.",
        ConsentType.TelehealthConsent =>
            "I consent to receive physical therapy services via telehealth, and understand its benefits, " +
            "limitations, and the alternative of an in-person visit.",
        ConsentType.DryNeedlingConsent =>
            "I consent to dry needling as part of my treatment plan, and have been informed of its risks, " +
            "including bruising, soreness, and rare risk of pneumothorax.",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

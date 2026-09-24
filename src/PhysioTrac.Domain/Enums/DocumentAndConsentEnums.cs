namespace PhysioTrac.Domain.Enums;

public enum DocumentCategory
{
    IntakeForm,
    InsuranceCard,
    Identification,
    ReferralOrder,
    ImagingReport,
    ClinicalDocument,
    Other,
}

/// <summary>The fixed consent types this clinic requires. Clinic-customizable
/// consent language is now a real config surface -- see ConsentTemplate --
/// ConsentTypeText's constants remain only as the platform-wide fallback
/// used until an organization configures its own template.</summary>
public enum ConsentType
{
    HipaaAcknowledgment,
    FinancialPolicy,
    ConsentToTreat,
    TelehealthConsent,
    DryNeedlingConsent,
}

/// <summary>Reviewed means clinic staff has looked over a submitted intake
/// form -- distinct from ClinicalNote's Draft/Signed lifecycle, since an
/// intake form is patient-authored data being acknowledged, not a clinical
/// document being finalized.</summary>
public enum IntakeFormSubmissionStatus
{
    Submitted,
    Reviewed,
}

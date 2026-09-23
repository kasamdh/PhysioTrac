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
/// consent language (a real "NoteTemplates"-style config surface) is a later
/// module -- for now each type's language is a simple versioned constant in
/// ConsentTypeText, not editable per-clinic.</summary>
public enum ConsentType
{
    HipaaAcknowledgment,
    FinancialPolicy,
    ConsentToTreat,
    TelehealthConsent,
    DryNeedlingConsent,
}

namespace PhysioTrac.Domain.Enums;

public enum InsuranceRank
{
    Primary,
    Secondary,
    Tertiary,
}

public enum RelationshipToSubscriber
{
    Self,
    Spouse,
    Child,
    Other,
}

public enum ChargeStatus
{
    Draft,
    Ready,
    Billed,
    Void,
}

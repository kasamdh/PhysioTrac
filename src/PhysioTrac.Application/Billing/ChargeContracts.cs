using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Billing;

public record ChargeDto(
    Guid Id, Guid PatientId, Guid? ClinicalNoteId, Guid? AppointmentId, Guid ProviderId, Guid? LocationId,
    DateOnly ServiceDate, string CptCode, IReadOnlyList<string> Modifiers, int Units, int? Minutes,
    int? RecommendedUnits, int? UnitsDifference, string? UnitsOverrideReason, decimal ChargeAmount, ChargeStatus Status);

public record CreateChargeRequest(
    Guid PatientId, Guid? ClinicalNoteId, Guid ProviderId, Guid? LocationId,
    DateOnly ServiceDate, string CptCode, IReadOnlyList<string>? Modifiers, int Units, int? Minutes,
    string? UnitsOverrideReason, decimal ChargeAmount, IReadOnlyList<Guid>? DiagnosisCodeIds);

public record UpdateChargeStatusRequest(ChargeStatus Status);

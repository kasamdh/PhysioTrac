using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Clinical;

/// <summary>Entity-to-DTO mapping for clinical notes (APIs never expose entities).</summary>
public static class ClinicalNoteMapper
{
    public static ClinicalNoteDto ToDto(ClinicalNote n) => new(
        n.Id, n.PatientId, n.TherapistId, n.AppointmentId, n.NoteType, n.Status, n.ServiceDate,
        n.Subjective, n.Objective, n.Interventions, n.Assessment, n.Plan,
        n.PlanOfCareStart, n.PlanOfCareEnd, n.FrequencyPerWeek, n.DurationWeeks, n.ReassessmentDue,
        n.PlanOfCareCertifiedDate, n.PlanOfCareCertifyingProviderId,
        n.SignatureName, n.SignatureCredentials, n.SignedAt, n.SignatureIpAddress, n.SignatureHash,
        n.CosignRequired, n.CosignedById, n.CosignedAt,
        n.SubjectiveDetailsJson, n.ObjectiveMeasurementsJson, n.AmendsNoteId, n.AmendmentReason,
        n.TreatingProviderId, n.SupervisingProviderId, n.TemplateVersionId, n.PlanOfCareId,
        n.PeriodStart, n.PeriodEnd, n.ReturnReason, n.VoidReason, n.PrefilledAt, n.PrefillReviewedAt);
}

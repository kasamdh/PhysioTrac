using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>Direct port of `services.note_compliance_findings` — explainable
/// documentation checks; only findings marked <c>FinalizationBlocker</c>
/// prevent signing. A pure function over an already-loaded note, no DB access.</summary>
public static class NoteComplianceEvaluator
{
    public static IReadOnlyList<ComplianceFinding> Evaluate(ClinicalNote note, DateOnly? today = null)
    {
        var effectiveToday = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var findings = new List<ComplianceFinding>();

        // Communication, missed-visit and addendum notes record no treatment,
        // so the S/O/A/P checks don't apply to them.
        var isVisitNote = note.NoteType is not (NoteType.Communication or NoteType.MissedVisit or NoteType.Addendum);
        if (isVisitNote && string.IsNullOrWhiteSpace(note.Objective))
        {
            findings.Add(new ComplianceFinding("missing_objective", "high", "Objective findings are missing",
                "Document measurable tests, observations, or treatment response.", true));
        }
        if (isVisitNote && string.IsNullOrWhiteSpace(note.Assessment))
        {
            findings.Add(new ComplianceFinding("missing_assessment", "medium", "Assessment is missing",
                "Explain clinical reasoning and the patient response to treatment."));
        }
        if (isVisitNote && string.IsNullOrWhiteSpace(note.Plan))
        {
            findings.Add(new ComplianceFinding("missing_plan", "high", "Plan is missing",
                "Document the next-visit plan, progression, and needed follow-up.", true));
        }

        if (note.NoteType is NoteType.Evaluation or NoteType.Progress or NoteType.ReEvaluation or NoteType.Recertification)
        {
            var pocComplete = note.PlanOfCareStart is not null && note.PlanOfCareEnd is not null
                && note.FrequencyPerWeek is not null && note.DurationWeeks is not null;
            if (!pocComplete)
            {
                findings.Add(new ComplianceFinding("missing_poc", "high", "Plan-of-care details are incomplete",
                    "Include start/end dates, planned frequency, and duration.", true));
            }
        }

        if (note.ReassessmentDue is DateOnly due && due < effectiveToday)
        {
            findings.Add(new ComplianceFinding("reassessment_overdue", "high", "Required reassessment is overdue",
                "Review outcome measures and update the plan of care before finalizing.", true));
        }

        if (note.Status == NoteStatus.ReviewRequired && note.CosignRequired)
        {
            findings.Add(new ComplianceFinding("cosign_pending", "medium", "Supervising cosignature pending",
                "A PT or clinical director must cosign this note before it is final."));
        }
        else if (!note.IsSigned)
        {
            findings.Add(new ComplianceFinding("signature_pending", "medium", "Therapist signature pending",
                "Drafts are not final clinical documentation."));
        }

        return findings;
    }
}

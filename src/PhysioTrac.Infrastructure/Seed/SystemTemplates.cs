using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Enums;
using static PhysioTrac.Infrastructure.Seed.Tpl;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>The documentation templates shipped with the application, one
/// per note type. Changing a definition here publishes a new version of
/// that system template at the next start (SystemTemplateSeeder); notes
/// already written keep the version they used. Fields stored in the
/// note's S/O/A/P columns carry a noteColumn; clinical components
/// (measurements, interventions, goals...) keep their data in their own
/// structured tables.</summary>
public static class SystemTemplates
{
    public static IReadOnlyList<SystemTemplate> All { get; } =
    [
        InitialEvaluation(),
        DailySoap(),
        ProgressNote(),
        Reevaluation(),
        Recertification(),
        DischargeSummary(),
        Consultation(),
        Communication(),
        MissedVisit(),
        Addendum(),
        .. SpecialtyEvaluations(),
    ];

    // ------------------------------------------------------------------ evaluation

    public static SystemTemplate InitialEvaluation() => new(
        "initial-evaluation", "Initial Evaluation", NoteType.Evaluation, ClinicalSpecialty.General,
        "Physical therapy initial evaluation: history, examination, assessment and plan of care.",
        EvaluationSections([]));

    /// <summary>The evaluation's sections; a specialty variant inserts its
    /// own examination section before the assessment.</summary>
    public static IReadOnlyList<TemplateSectionDto> EvaluationSections(IReadOnlyList<TemplateSectionDto> specialtySections) =>
    [
        Section("encounter", "Encounter information", null, "Patient, appointment, therapist and visit type come from the schedule.",
            Select("visitType", "Visit type", "Initial evaluation", "Evaluation and treatment", "Second opinion").Req(),
            Short("referringProvider", "Referring provider"),
            Number("visitNumber", "Visit number", min: 1)),
        Section("diagnosis", "Diagnosis", "diagnoses", "ICD-10 codes are added to the patient's diagnosis list.",
            Short("medicalDiagnosis", "Medical diagnosis").Req(),
            Short("treatmentDiagnosis", "Treatment diagnosis").Req(),
            Date("onsetDate", "Date of onset"),
            Date("injuryDate", "Injury date"),
            Date("surgeryDate", "Surgery date"),
            Long("referralInformation", "Referral information")),
        Section("medical", "Medical information", "medicalHistory", "Medications and allergies come from the patient's record.",
            Long("medicalHistory", "Medical history"),
            Long("surgicalHistory", "Surgical history"),
            Long("precautions", "Precautions"),
            Long("contraindications", "Contraindications"),
            Radio("fallRisk", "Fall-risk screening", "Low risk", "Moderate risk", "High risk", "Not screened").Req(),
            Radio("redFlags", "Red-flag screening", "Negative", "Positive — addressed", "Positive — referred").Req(),
            Long("redFlagDetails", "Red-flag details").When("redFlags", "Positive — addressed").Req(),
            Long("referralForRedFlags", "Referral for red flags").When("redFlags", "Positive — referred").Req()),
        Section("subjective", "Subjective examination", "painAssessment", null,
            Short("chiefComplaint", "Primary complaint").Req(),
            Long("historyOfPresentCondition", "History of present condition", "subjective").Req(),
            Long("priorLevelOfFunction", "Prior level of function").Req(),
            Long("functionalLimitations", "Current functional limitations").Req(),
            Long("patientGoals", "Patient goals").Req(),
            Long("socialEnvironment", "Social environment"),
            Long("homeEnvironment", "Home environment").Hint("Stairs, rails, layout, support at home"),
            Long("employmentDemands", "Employment demands"),
            Long("activityDemands", "Sports and activity demands"),
            Long("homeExerciseHistory", "Home-exercise history")),
        Section("bodyChart", "Body chart", "bodyChart", "Mark where symptoms are and what they feel like."),
        Section("objective", "Objective examination", "measurements", null,
            Long("observation", "Observation", "objective").Req(),
            Long("posture", "Posture"),
            Long("gait", "Gait"),
            Long("sensation", "Sensation"),
            Long("reflexes", "Reflexes"),
            Long("tone", "Tone"),
            Long("coordination", "Coordination"),
            Long("balance", "Balance"),
            Long("functionalTests", "Functional tests"),
            Long("palpation", "Palpation")),
        Section("specialTests", "Special tests", "specialTests", null),
        Section("outcomes", "Outcome measures", "outcomes", "Standardized measures give the baseline for progress notes."),
        .. specialtySections,
        Section("assessment", "Assessment",
            Short("ptDiagnosis", "Physical therapy diagnosis").Req(),
            Long("clinicalInterpretation", "Clinical interpretation", "assessment").Req(),
            Long("problemList", "Problem list").Req(),
            Select("prognosis", "Prognosis", Prognoses).Req(),
            Select("rehabPotential", "Rehabilitation potential", "Excellent", "Good", "Fair", "Poor").Req(),
            Long("skilledNeed", "Skilled need").Req().Help("Why the patient needs a physical therapist's skills."),
            Long("barriers", "Barriers to recovery"),
            Long("facilitators", "Facilitators of recovery")),
        Section("goals", "Goals", "goals", "Short- and long-term goals: functional, measurable and time-bound."),
        Section("planOfCare", "Plan of care", "planOfCare", "Signing the evaluation creates the patient's plan of care.",
            Number("frequencyPerWeek", "Visit frequency (per week)", "visits", 1, 7).Req(),
            Number("durationWeeks", "Duration (weeks)", "weeks", 1, 52).Req(),
            Multi("plannedInterventions", "Planned interventions",
                "Therapeutic exercise", "Therapeutic activity", "Neuromuscular re-education", "Manual therapy", "Gait training",
                "Self-care / home management", "Modalities", "Dry needling", "Canalith repositioning", "Patient education").Req(),
            Long("homeExerciseProgram", "Home exercise program"),
            Long("patientEducation", "Patient education").Req(),
            Long("referrals", "Referrals"),
            Long("planSummary", "Plan summary", "plan").Req(),
            Date("certificationStart", "Certification start").Req(),
            Date("certificationEnd", "Certification end").Req(),
            Signature()),
    ];

    /// <summary>Initial evaluations for each specialty: the shared
    /// evaluation plus a specialty examination section.</summary>
    public static IReadOnlyList<SystemTemplate> SpecialtyEvaluations() =>
    [
        Specialty(ClinicalSpecialty.Orthopedic, "orthopedic", "Orthopedic",
            Section("orthoExam", "Orthopedic examination",
                Select("region", "Primary region", "Cervical", "Thoracic", "Lumbar", "Shoulder", "Elbow", "Wrist/hand", "Hip", "Knee", "Ankle/foot").Req(),
                Long("jointIntegrity", "Joint integrity and mobility"),
                Long("muscleLength", "Muscle length / flexibility"),
                Long("movementPatterns", "Movement patterns"))),
        Specialty(ClinicalSpecialty.Neurological, "neurological", "Neurological",
            Section("neuroExam", "Neurological examination",
                Short("neuroDiagnosis", "Neurological diagnosis").Req(),
                Long("cognition", "Cognition and communication"),
                Long("cranialNerves", "Cranial nerve findings"),
                Long("motorControl", "Motor control and synergy patterns"),
                Long("transfersBedMobility", "Bed mobility and transfers").Req(),
                Select("assistanceLevel", "Overall assistance level", AssistanceLevels))),
        Specialty(ClinicalSpecialty.PelvicHealth, "pelvic-health", "Pelvic Health",
            Section("pelvicExam", "Pelvic health examination", null, "Document consent before any internal examination.",
                Check("consentObtained", "Consent for examination obtained").Req(),
                Long("bladderFunction", "Bladder function"),
                Long("bowelFunction", "Bowel function"),
                Long("sexualFunction", "Sexual function"),
                Long("pelvicFloorAssessment", "Pelvic floor assessment"),
                Short("obstetricHistory", "Obstetric history"))),
        Specialty(ClinicalSpecialty.SportsRehabilitation, "sports", "Sports Rehabilitation",
            Section("sportsExam", "Sports examination",
                Short("sport", "Sport and position").Req(),
                Short("competitionLevel", "Competition level"),
                Date("returnTarget", "Target return-to-sport date"),
                Long("sportSpecificTesting", "Sport-specific testing"),
                Long("loadTolerance", "Load tolerance"))),
        Specialty(ClinicalSpecialty.PostSurgical, "post-surgical", "Post-Surgical Rehabilitation",
            Section("surgicalExam", "Post-surgical examination",
                Short("procedure", "Procedure").Req(),
                Short("surgeon", "Surgeon"),
                Short("protocol", "Protocol / restrictions").Req(),
                Select("weightBearing", "Weight-bearing status", "Full", "Weight-bearing as tolerated", "Partial", "Toe-touch", "Non-weight-bearing").Req(),
                Long("incision", "Incision and wound status"),
                Long("swelling", "Swelling / edema"))),
        Specialty(ClinicalSpecialty.GaitAndBalance, "gait-balance", "Gait and Balance",
            Section("balanceExam", "Gait and balance examination",
                Number("fallsLastYear", "Falls in the last 12 months", min: 0).Req(),
                Short("assistiveDevice", "Assistive device"),
                Measure("gaitSpeed", "Gait speed", "m/s", 0, 3),
                Long("staticBalance", "Static balance"),
                Long("dynamicBalance", "Dynamic balance"),
                Long("fearOfFalling", "Fear of falling"))),
        Specialty(ClinicalSpecialty.Vestibular, "vestibular", "Vestibular",
            Section("vestibularExam", "Vestibular examination",
                Multi("symptoms", "Symptoms", "Vertigo", "Dizziness", "Imbalance", "Nausea", "Visual disturbance").Req(),
                Long("oculomotor", "Oculomotor exam"),
                Radio("dixHallpike", "Dix-Hallpike", "Not tested", "Negative", "Positive right", "Positive left"),
                Long("headImpulse", "Head impulse test"),
                Long("motionSensitivity", "Motion sensitivity"))),
        Specialty(ClinicalSpecialty.Cardiopulmonary, "cardiopulmonary", "Cardiopulmonary",
            Section("cardioExam", "Cardiopulmonary examination", null, "Record vital signs at rest and with activity.",
                Short("restingVitals", "Resting vitals").Req(),
                Short("exerciseVitals", "Vitals with activity"),
                Measure("spo2Rest", "SpO2 at rest", "%", 50, 100),
                Select("dyspneaScale", "Dyspnea (Borg)", "0", "0.5", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"),
                Long("enduranceTesting", "Endurance testing"),
                Long("breathingPattern", "Breathing pattern"))),
        Specialty(ClinicalSpecialty.Tmj, "tmj", "TMJ",
            Section("tmjExam", "TMJ examination",
                Measure("mouthOpening", "Mouth opening", "mm", 0, 80).Req(),
                Long("jointSounds", "Joint sounds"),
                Long("deviation", "Deviation / deflection"),
                Long("parafunction", "Parafunctional habits"))),
        Specialty(ClinicalSpecialty.PersistentPain, "persistent-pain", "Persistent Pain",
            Section("painExam", "Persistent pain assessment",
                Short("painDuration", "Pain duration").Req(),
                Long("painBeliefs", "Pain beliefs and coping"),
                Long("sleep", "Sleep"),
                Long("activityPacing", "Activity and pacing"),
                Long("psychosocialFactors", "Psychosocial factors (yellow flags)"))),
        Specialty(ClinicalSpecialty.DryNeedling, "dry-needling", "Dry Needling",
            Section("dryNeedlingScreen", "Dry needling screening", null, "Dry needling consent is recorded in the patient's consents.",
                Check("consentOnFile", "Dry needling consent on file").Req(),
                Radio("needleSensitivity", "Needle sensitivity or phobia", YesNo).Req(),
                Radio("bleedingRisk", "Bleeding disorder or anticoagulants", YesNo).Req(),
                Radio("pregnancy", "Pregnancy", "Yes", "No", "Not applicable").Req(),
                Long("contraindicationNotes", "Contraindication notes").When("bleedingRisk", "Yes").Req(),
                Long("targetMuscles", "Target muscles"))),
    ];

    private static SystemTemplate Specialty(ClinicalSpecialty specialty, string key, string label, TemplateSectionDto section) => new(
        $"initial-evaluation-{key}", $"Initial Evaluation — {label}", NoteType.Evaluation, specialty,
        $"Initial evaluation with the {label.ToLowerInvariant()} examination.",
        EvaluationSections([section]));

    // ------------------------------------------------------------------ daily

    public static SystemTemplate DailySoap() => new(
        "daily-soap", "Daily Treatment SOAP Note", NoteType.Daily, ClinicalSpecialty.General,
        "Treatment visit: subjective report, measurements and interventions, assessment and plan.",
        [
            Section("subjective", "Subjective", "painAssessment", null,
                Long("patientReport", "Patient report", "subjective").Req(),
                Long("functionalChanges", "Functional changes"),
                Radio("hepCompliance", "Home-program compliance", "Doing it", "Partly", "Not doing it", "No program yet"),
                Long("healthChanges", "Health changes"),
                Long("medicationChanges", "Medication changes"),
                Radio("adverseEvents", "Adverse events since last visit", YesNo),
                Long("adverseEventDetails", "Adverse event details").When("adverseEvents", "Yes").Req()),
            Section("bodyChart", "Pain and body chart", "bodyChart", null),
            Section("objective", "Objective measurements", "measurements", null,
                Long("objectiveFindings", "Objective findings", "objective").Req()),
            Section("tests", "Tests and measures", "specialTests", null),
            Section("interventions", "Interventions", "interventions", "Treatment time and units are totaled from the flowsheet.",
                Select("assistanceLevel", "Assistance level", AssistanceLevels),
                Short("equipment", "Equipment"),
                Long("patientResponse", "Patient response to treatment").Req(),
                Long("treatmentSummary", "Treatment summary", "interventions")),
            Section("goals", "Goals", "goals", null),
            Section("outcomes", "Outcome measures", "outcomes", null),
            Section("assessment", "Assessment",
                Long("responseToTreatment", "Response to treatment"),
                Long("progressTowardGoals", "Progress toward goals"),
                Long("remainingImpairments", "Remaining impairments"),
                Long("functionalLimitations", "Functional limitations"),
                Long("clinicalReasoning", "Clinical reasoning", "assessment").Req(),
                Long("continuedSkilledNeed", "Continued skilled need").Req(),
                Long("changeFromPrevious", "Change from previous visit")),
            Section("plan", "Plan",
                Long("nextVisitPlan", "Next-visit plan", "plan").Req(),
                Long("treatmentProgression", "Treatment progression"),
                Short("frequency", "Frequency"),
                Long("hepChanges", "Home-program changes"),
                Long("referrals", "Referrals"),
                Long("providerCommunication", "Provider communication")),
            Section("signature", "Signature", Signature()),
        ]);

    // ------------------------------------------------------------------ progress / re-eval / recert / discharge

    public static SystemTemplate ProgressNote() => new(
        "progress-note", "Progress Note", NoteType.Progress, ClinicalSpecialty.General,
        "Progress report for the reporting period: changes, goals, outcomes and updated plan.",
        [
            Section("period", "Reporting period",
                Date("periodStart", "Period start").Req(),
                Date("periodEnd", "Period end").Req(),
                Number("visitsCompleted", "Visits completed", min: 0).Req()),
            Section("subjective", "Subjective changes", "painAssessment", null,
                Long("subjectiveChanges", "Subjective changes", "subjective").Req()),
            Section("objective", "Objective changes", "measurements", "Baseline, previous and current values are shown for comparison.",
                Long("objectiveChanges", "Objective changes", "objective").Req()),
            Section("goals", "Goal progress", "goals", null),
            Section("outcomes", "Outcome-measure change", "outcomes", null),
            Section("assessment", "Assessment",
                Long("functionalImprovement", "Functional improvement").Req(),
                Long("remainingImpairments", "Remaining impairments").Req(),
                Long("continuedSkilledNeed", "Continued skilled need", "assessment").Req(),
                Select("updatedPrognosis", "Updated prognosis", Prognoses)),
            Section("plan", "Updated plan", "planOfCare", null,
                Long("updatedGoals", "Updated goals"),
                Long("updatedPlan", "Updated plan", "plan").Req(),
                Number("frequencyPerWeek", "Frequency (per week)", "visits", 1, 7).Req(),
                Number("durationWeeks", "Duration (weeks)", "weeks", 1, 52).Req(),
                Signature()),
        ]);

    public static SystemTemplate Reevaluation() => new(
        "reevaluation", "Reevaluation", NoteType.ReEvaluation, ClinicalSpecialty.General,
        "Formal reevaluation after a change in condition; signing creates a new plan-of-care version.",
        ReevaluationSections(recertification: false));

    public static SystemTemplate Recertification() => new(
        "recertification", "Recertification", NoteType.Recertification, ClinicalSpecialty.General,
        "Recertifies the plan of care for a new certification period.",
        ReevaluationSections(recertification: true));

    private static IReadOnlyList<TemplateSectionDto> ReevaluationSections(bool recertification) =>
    [
        Section("reason", recertification ? "Reason for recertification" : "Reason for reevaluation",
            Long("reason", "Reason").Req(),
            Long("changeInCondition", "Change in condition").Req()),
        Section("subjective", "Updated subjective status", "painAssessment", null,
            Long("updatedSubjective", "Updated subjective status", "subjective").Req()),
        Section("objective", "Updated objective examination", "measurements", null,
            Long("updatedObjective", "Updated objective examination", "objective").Req(),
            Long("comparisonWithEvaluation", "Comparison with evaluation").Req(),
            Long("comparisonWithProgress", "Comparison with previous progress note")),
        Section("specialTests", "Special tests", "specialTests", null),
        Section("outcomes", "Updated outcomes", "outcomes", null),
        Section("goals", "Updated goals", "goals", null),
        Section("assessment", "Updated assessment",
            Long("updatedAssessment", "Updated assessment", "assessment").Req(),
            Select("updatedPrognosis", "Updated prognosis", Prognoses).Req()),
        Section("planOfCare", "Updated plan of care", "planOfCare", "Signing creates a new plan of care; the previous one is kept.",
            Long("updatedPlan", "Updated plan of care", "plan").Req(),
            Number("frequencyPerWeek", "Frequency (per week)", "visits", 1, 7).Req(),
            Number("durationWeeks", "Duration (weeks)", "weeks", 1, 52).Req(),
            Date("certificationStart", "Certification start").Req(),
            Date("certificationEnd", "Certification end").Req(),
            Signature()),
    ];

    public static SystemTemplate DischargeSummary() => new(
        "discharge-summary", "Discharge Summary", NoteType.Discharge, ClinicalSpecialty.General,
        "Closes the episode of care and the plan of care.",
        [
            Section("discharge", "Discharge",
                Select("dischargeReason", "Reason for discharge",
                    "Goals met", "Maximum benefit achieved", "Patient request", "Nonattendance", "Medical change",
                    "Referred elsewhere", "Authorization limitation", "Other documented reason").Req(),
                Long("otherReason", "Other reason").When("dischargeReason", "Other documented reason").Req(),
                Number("visitsCompleted", "Visits completed", min: 0).Req(),
                Short("attendance", "Attendance").Hint("e.g. 11 of 12 scheduled visits")),
            Section("subjective", "Final subjective status", "painAssessment", null,
                Long("finalSubjective", "Final subjective status", "subjective").Req()),
            Section("objective", "Final objective measurements", "measurements", null,
                Long("finalObjective", "Final objective measurements", "objective").Req()),
            Section("outcomes", "Final outcome measures", "outcomes", null),
            Section("goals", "Goal status", "goals", null),
            Section("assessment", "Outcome",
                Long("functionalOutcome", "Functional outcome").Req(),
                Long("therapistAssessment", "Therapist assessment", "assessment").Req()),
            Section("plan", "Recommendations",
                Long("homeProgram", "Home program").Req(),
                Long("selfManagement", "Self-management education"),
                Long("followUp", "Follow-up recommendations", "plan").Req(),
                Long("referralRecommendations", "Referral recommendations"),
                Signature()),
        ]);

    // ------------------------------------------------------------------ other notes

    public static SystemTemplate Consultation() => new(
        "consultation", "Consultation Note", NoteType.Consultation, ClinicalSpecialty.General,
        "A consult requested by another provider.",
        [
            Section("consult", "Consultation",
                Short("requestedBy", "Requested by").Req(),
                Long("reasonForConsult", "Reason for consultation").Req(),
                Long("findings", "Findings", "objective").Req(),
                Long("impression", "Impression", "assessment").Req(),
                Long("recommendations", "Recommendations", "plan").Req(),
                Signature()),
        ]);

    public static SystemTemplate Communication() => new(
        "communication", "Communication or Telephone Note", NoteType.Communication, ClinicalSpecialty.General,
        "A call, message or conversation about the patient's care.",
        [
            Section("communication", "Communication",
                Radio("method", "Method", "Telephone", "Portal message", "Email", "In person", "Fax").Req(),
                Short("contact", "With whom").Req(),
                Time("time", "Time"),
                Long("summary", "Summary", "subjective").Req(),
                Long("actionTaken", "Action taken", "plan"),
                Signature()),
        ]);

    public static SystemTemplate MissedVisit() => new(
        "missed-visit", "Missed Visit or Cancellation Note", NoteType.MissedVisit, ClinicalSpecialty.General,
        "Documents a cancelled or missed visit. No treatment is recorded.",
        [
            Section("missed", "Missed visit",
                Radio("type", "Type", "Cancelled", "No-show", "Late cancellation").Req(),
                Short("reason", "Reason given"),
                Check("patientContacted", "Patient contacted"),
                Long("contactDetails", "Contact details").When("patientContacted", "true"),
                Check("rescheduled", "Visit rescheduled"),
                Long("impactOnPlan", "Impact on the plan of care", "plan"),
                Signature()),
        ]);

    public static SystemTemplate Addendum() => new(
        "addendum", "Addendum or Amendment", NoteType.Addendum, ClinicalSpecialty.General,
        "A late entry or correction related to a signed note.",
        [
            Section("addendum", "Addendum",
                Date("relatedServiceDate", "Related service date").Req(),
                Radio("kind", "Kind", "Late entry", "Clarification", "Correction").Req(),
                Long("reason", "Reason").Req(),
                Long("content", "Content", "subjective").Req(),
                Signature()),
        ]);
}

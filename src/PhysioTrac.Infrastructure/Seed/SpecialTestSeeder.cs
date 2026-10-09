using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>The built-in special-test library. Reference data only (no
/// patient data); interpretation notes are guidance for the therapist, never
/// a diagnosis. Runs at every start: adds missing tests and refreshes the
/// text of built-in ones; never removes a test or changes its active flag.</summary>
public static class SpecialTestSeeder
{
    private const SpecialTestResultKind PN = SpecialTestResultKind.PositiveNegative;
    private const SpecialTestResultKind Num = SpecialTestResultKind.Numeric;
    private const SpecialTestResultKind Both = SpecialTestResultKind.Both;

    private const string Combine = " Interpret together with the history and other findings.";
    private const string CervicalCaution =
        "Screen for cervical instability, vertebrobasilar insufficiency and recent neck trauma before testing.";

    public sealed record Def(string Code, string Name, ClinicalSpecialty Specialty, string Region, SpecialTestResultKind Kind,
        string? Unit = null, string? Guide = null, string? Warning = null);

    public static IReadOnlyList<Def> All { get; } =
    [
        // Orthopedic -- knee
        new("lachman", "Lachman test", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Increased anterior tibial translation or a soft end feel may be consistent with ACL insufficiency." + Combine),
        new("anterior-drawer-knee", "Anterior drawer (knee)", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Increased anterior translation may be consistent with ACL insufficiency." + Combine),
        new("posterior-drawer-knee", "Posterior drawer (knee)", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Increased posterior translation may be consistent with PCL insufficiency." + Combine),
        new("mcmurray", "McMurray test", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "A painful click along the joint line may be consistent with meniscal involvement." + Combine),
        new("thessaly", "Thessaly test", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Joint-line pain or locking during rotation may be consistent with meniscal involvement." + Combine),
        new("valgus-stress-knee", "Valgus stress test (knee)", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Medial gapping or pain may be consistent with MCL involvement." + Combine),
        new("varus-stress-knee", "Varus stress test (knee)", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Lateral gapping or pain may be consistent with LCL involvement." + Combine),
        new("patellar-apprehension", "Patellar apprehension test", ClinicalSpecialty.Orthopedic, "Knee", PN, Guide: "Apprehension with lateral patellar glide may be consistent with patellar instability." + Combine),
        // Orthopedic -- shoulder
        new("neer", "Neer impingement test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Pain with passive elevation may be consistent with subacromial pain." + Combine),
        new("hawkins-kennedy", "Hawkins-Kennedy test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Pain with internal rotation in flexion may be consistent with subacromial pain." + Combine),
        new("empty-can", "Empty can (Jobe) test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Weakness or pain may be consistent with supraspinatus involvement." + Combine),
        new("drop-arm", "Drop arm test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Inability to lower the arm smoothly may be consistent with a rotator cuff tear." + Combine),
        new("speed", "Speed test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Bicipital groove pain may be consistent with biceps tendon involvement." + Combine),
        new("obrien", "O'Brien active compression test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Deep pain relieved in supination may be consistent with labral involvement." + Combine),
        new("apprehension-shoulder", "Apprehension / relocation test", ClinicalSpecialty.Orthopedic, "Shoulder", PN, Guide: "Apprehension relieved by relocation may be consistent with anterior instability." + Combine,
            Warning: "Avoid after a recent dislocation or stabilization surgery without surgeon clearance."),
        // Orthopedic -- spine, hip, ankle, wrist, elbow
        new("spurling", "Spurling test", ClinicalSpecialty.Orthopedic, "Cervical", PN, Guide: "Reproduction of arm symptoms may be consistent with cervical radiculopathy." + Combine, Warning: CervicalCaution),
        new("cervical-distraction", "Cervical distraction test", ClinicalSpecialty.Orthopedic, "Cervical", PN, Guide: "Relief of arm symptoms may be consistent with cervical radiculopathy." + Combine, Warning: CervicalCaution),
        new("slr", "Straight leg raise", ClinicalSpecialty.Orthopedic, "Lumbar", Both, "deg", "Leg symptoms between about 30 and 70 degrees may be consistent with lumbar nerve-root irritation." + Combine),
        new("slump", "Slump test", ClinicalSpecialty.Orthopedic, "Lumbar", PN, Guide: "Symptoms changed by neck flexion may be consistent with neural mechanosensitivity." + Combine),
        new("faber", "FABER (Patrick) test", ClinicalSpecialty.Orthopedic, "Hip", PN, Guide: "Groin pain may suggest hip involvement; posterior pain may suggest sacroiliac involvement." + Combine),
        new("fadir", "FADIR test", ClinicalSpecialty.Orthopedic, "Hip", PN, Guide: "Anterior hip pain may be consistent with femoroacetabular impingement or labral involvement." + Combine),
        new("thomas", "Thomas test", ClinicalSpecialty.Orthopedic, "Hip", PN, Guide: "Thigh lifting off the table may be consistent with hip flexor tightness." + Combine),
        new("ober", "Ober test", ClinicalSpecialty.Orthopedic, "Hip", PN, Guide: "The leg staying abducted may be consistent with iliotibial band tightness." + Combine),
        new("trendelenburg", "Trendelenburg sign", ClinicalSpecialty.Orthopedic, "Hip", PN, Guide: "Pelvic drop may be consistent with hip abductor weakness." + Combine),
        new("anterior-drawer-ankle", "Anterior drawer (ankle)", ClinicalSpecialty.Orthopedic, "Ankle", PN, Guide: "Increased anterior talar translation may be consistent with ATFL insufficiency." + Combine),
        new("talar-tilt", "Talar tilt test", ClinicalSpecialty.Orthopedic, "Ankle", PN, Guide: "Increased inversion tilt may be consistent with CFL involvement." + Combine),
        new("thompson", "Thompson test", ClinicalSpecialty.Orthopedic, "Ankle", PN, Guide: "No plantarflexion with calf squeeze may be consistent with Achilles rupture -- refer promptly." + Combine),
        new("phalen", "Phalen test", ClinicalSpecialty.Orthopedic, "Wrist/hand", PN, Guide: "Median-nerve symptoms within a minute may be consistent with carpal tunnel involvement." + Combine),
        new("tinel-carpal", "Tinel sign (carpal tunnel)", ClinicalSpecialty.Orthopedic, "Wrist/hand", PN, Guide: "Tingling in the median distribution may be consistent with carpal tunnel involvement." + Combine),
        new("finkelstein", "Finkelstein test", ClinicalSpecialty.Orthopedic, "Wrist/hand", PN, Guide: "Radial wrist pain may be consistent with De Quervain tenosynovitis." + Combine),
        new("cozen", "Cozen test", ClinicalSpecialty.Orthopedic, "Elbow", PN, Guide: "Lateral elbow pain with resisted extension may be consistent with lateral elbow tendinopathy." + Combine),
        // Neurological
        new("babinski", "Babinski sign", ClinicalSpecialty.Neurological, "Foot", PN, Guide: "Great-toe extension with fanning is an upper motor neuron sign in adults -- refer for medical evaluation." + Combine),
        new("hoffmann", "Hoffmann sign", ClinicalSpecialty.Neurological, "Hand", PN, Guide: "Thumb/index flexion may be an upper motor neuron sign -- consider medical referral." + Combine),
        new("ankle-clonus", "Ankle clonus", ClinicalSpecialty.Neurological, "Ankle", Both, "beats", "Sustained clonus (more than a few beats) may be an upper motor neuron sign." + Combine),
        new("finger-to-nose", "Finger-to-nose test", ClinicalSpecialty.Neurological, "Upper extremity", PN, Guide: "Dysmetria or intention tremor may be consistent with cerebellar involvement." + Combine),
        new("heel-to-shin", "Heel-to-shin test", ClinicalSpecialty.Neurological, "Lower extremity", PN, Guide: "Inaccuracy may be consistent with cerebellar or proprioceptive involvement." + Combine),
        new("rapid-alternating", "Rapid alternating movements", ClinicalSpecialty.Neurological, "Upper extremity", PN, Guide: "Dysdiadochokinesia may be consistent with cerebellar involvement." + Combine),
        // Vestibular
        new("dix-hallpike", "Dix-Hallpike test", ClinicalSpecialty.Vestibular, "Vestibular", PN, Guide: "Torsional up-beating nystagmus with latency may be consistent with posterior canal BPPV." + Combine, Warning: CervicalCaution),
        new("roll-test", "Supine roll test", ClinicalSpecialty.Vestibular, "Vestibular", PN, Guide: "Horizontal nystagmus may be consistent with horizontal canal BPPV." + Combine, Warning: CervicalCaution),
        new("head-impulse", "Head impulse test", ClinicalSpecialty.Vestibular, "Vestibular", PN, Guide: "A corrective saccade may be consistent with peripheral vestibular hypofunction." + Combine, Warning: CervicalCaution),
        new("head-shake-nystagmus", "Head-shake nystagmus", ClinicalSpecialty.Vestibular, "Vestibular", PN, Guide: "Nystagmus after head shaking may be consistent with asymmetric vestibular function." + Combine, Warning: CervicalCaution),
        new("dynamic-visual-acuity", "Dynamic visual acuity", ClinicalSpecialty.Vestibular, "Vestibular", Num, "lines lost", "A loss of three or more lines may be consistent with reduced vestibulo-ocular function." + Combine),
        // Balance
        new("romberg", "Romberg test", ClinicalSpecialty.GaitAndBalance, "Balance", PN, Guide: "Loss of balance with eyes closed may be consistent with a proprioceptive deficit." + Combine),
        new("sharpened-romberg", "Sharpened (tandem) Romberg", ClinicalSpecialty.GaitAndBalance, "Balance", Num, "sec"),
        new("single-leg-stance", "Single-leg stance", ClinicalSpecialty.GaitAndBalance, "Balance", Num, "sec", "Shorter times may be associated with higher fall risk in older adults." + Combine),
        new("functional-reach", "Functional reach test", ClinicalSpecialty.GaitAndBalance, "Balance", Num, "in", "Shorter reach may be associated with higher fall risk." + Combine),
        new("mctsib", "Modified CTSIB (4 conditions)", ClinicalSpecialty.GaitAndBalance, "Balance", Num, "sec", "Record total seconds across the four conditions (maximum 120)." + Combine),
        // Pelvic health
        new("modified-oxford", "Modified Oxford scale (pelvic floor)", ClinicalSpecialty.PelvicHealth, "Pelvic floor", Num, "grade 0-5",
            Warning: "Internal assessment requires documented informed consent and appropriate training."),
        new("cough-stress-test", "Cough stress test", ClinicalSpecialty.PelvicHealth, "Pelvic floor", PN, Guide: "Leakage with cough may be consistent with stress urinary incontinence." + Combine),
        new("active-slr-pelvic", "Active straight leg raise (pelvic girdle)", ClinicalSpecialty.PelvicHealth, "Pelvis", Num, "score 0-10", "Higher scores reflect more difficulty lifting the leg." + Combine),
        new("p4-test", "Posterior pelvic pain provocation (P4)", ClinicalSpecialty.PelvicHealth, "Pelvis", PN, Guide: "Deep gluteal pain may be consistent with pelvic girdle pain." + Combine),
        new("diastasis-recti", "Inter-rectus distance", ClinicalSpecialty.PelvicHealth, "Abdomen", Num, "finger widths"),
        // Sports performance
        new("single-hop", "Single-leg hop for distance", ClinicalSpecialty.SportsRehabilitation, "Lower extremity", Num, "cm", "Compare sides as a limb symmetry index (involved / uninvolved x 100)." + Combine),
        new("triple-hop", "Triple hop for distance", ClinicalSpecialty.SportsRehabilitation, "Lower extremity", Num, "cm"),
        new("y-balance", "Y-Balance test (composite)", ClinicalSpecialty.SportsRehabilitation, "Lower extremity", Num, "% limb length"),
        new("drop-vertical-jump", "Drop vertical jump (dynamic valgus)", ClinicalSpecialty.SportsRehabilitation, "Knee", PN, Guide: "Visible dynamic knee valgus on landing may indicate a movement pattern to train." + Combine),
        new("ckcuest", "Closed kinetic chain upper extremity stability test", ClinicalSpecialty.SportsRehabilitation, "Upper extremity", Num, "touches"),
        // Cardiopulmonary
        new("six-minute-walk", "Six-minute walk test", ClinicalSpecialty.Cardiopulmonary, "Endurance", Num, "m",
            Warning: "Stop for chest pain, severe dyspnea, dizziness or SpO2 below the agreed threshold."),
        new("two-minute-step", "Two-minute step test", ClinicalSpecialty.Cardiopulmonary, "Endurance", Num, "steps"),
        new("borg-rpe", "Borg rating of perceived exertion", ClinicalSpecialty.Cardiopulmonary, "Exertion", Num, "6-20"),
        new("orthostatic-vitals", "Orthostatic vital signs", ClinicalSpecialty.Cardiopulmonary, "Vitals", Both, "mmHg drop",
            Guide: "A sustained systolic drop of 20 mmHg or more on standing may indicate orthostatic hypotension." + Combine),
        // TMJ
        new("max-mouth-opening", "Maximal mouth opening", ClinicalSpecialty.Tmj, "TMJ", Num, "mm"),
        new("tmj-joint-sounds", "TMJ joint sounds", ClinicalSpecialty.Tmj, "TMJ", PN, Guide: "Clicking or crepitus may be consistent with disc or joint changes." + Combine),
        new("tmj-loading", "TMJ dynamic loading", ClinicalSpecialty.Tmj, "TMJ", PN, Guide: "Pain with loading may be consistent with joint involvement." + Combine),
        // Post-surgical
        new("extensor-lag", "Quadriceps extensor lag", ClinicalSpecialty.PostSurgical, "Knee", Num, "deg"),
        new("effusion-stroke", "Knee effusion (stroke test)", ClinicalSpecialty.PostSurgical, "Knee", PN, Guide: "A fluid wave may indicate joint effusion; grade it in the comments." + Combine),
        new("wells-dvt", "Wells score for DVT", ClinicalSpecialty.PostSurgical, "Lower extremity", Num, "points",
            Guide: "A higher score raises the probability of DVT.",
            Warning: "Suspected DVT is a medical emergency: stop treatment and refer immediately."),
        // Dry-needling screening
        new("dn-contraindication-screen", "Dry needling contraindication screen", ClinicalSpecialty.DryNeedling, "Screening", PN,
            Guide: "Positive = a contraindication was found; do not needle until resolved.",
            Warning: "Do not needle over a pregnant uterus, active infection, lymphedema, an open wound, or with a needle phobia or without consent."),
        new("dn-bleeding-risk", "Bleeding risk screen", ClinicalSpecialty.DryNeedling, "Screening", PN,
            Guide: "Positive = anticoagulants or a bleeding disorder; adjust or defer needling.",
            Warning: "Check anticoagulant use and bleeding disorders before needling."),
        new("dn-vasovagal-history", "Vasovagal / needle reaction history", ClinicalSpecialty.DryNeedling, "Screening", PN,
            Guide: "Positive = a history of fainting with needles; treat lying down and monitor."),
    ];

    public static async Task SeedAsync(PhysioTracDbContext db, CancellationToken ct = default)
    {
        var existing = await db.SpecialTestDefinitions.Where(d => d.IsSystem).ToDictionaryAsync(d => d.Code, ct);
        foreach (var d in All)
        {
            if (!existing.TryGetValue(d.Code, out var row))
            {
                row = new SpecialTestDefinition { Code = d.Code, IsSystem = true };
                db.SpecialTestDefinitions.Add(row);
            }
            row.Name = d.Name;
            row.Specialty = d.Specialty;
            row.BodyRegion = d.Region;
            row.ResultKind = d.Kind;
            row.Unit = d.Unit;
            row.InterpretationGuide = d.Guide;
            row.ContraindicationWarning = d.Warning;
        }
        await db.SaveChangesAsync(ct);
    }
}

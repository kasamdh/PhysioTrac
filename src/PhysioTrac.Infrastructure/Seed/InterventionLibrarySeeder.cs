using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>The built-in intervention library (reference data, no patient
/// data). CPT codes are the usual ones for each service and are suggestions
/// only -- the clinic's own coding policy and payer rules decide what is
/// billed. Adds missing items and refreshes built-in text at every start;
/// never removes an item or changes its active flag.</summary>
public static class InterventionLibrarySeeder
{
    private const InterventionCategory Ex = InterventionCategory.TherapeuticExercise;
    private const InterventionCategory Act = InterventionCategory.TherapeuticActivity;
    private const InterventionCategory Nmr = InterventionCategory.NeuromuscularReeducation;
    private const InterventionCategory Man = InterventionCategory.ManualTherapy;
    private const InterventionCategory Gait = InterventionCategory.GaitTraining;
    private const InterventionCategory Self = InterventionCategory.SelfCare;
    private const InterventionCategory Crp = InterventionCategory.CanalithRepositioning;
    private const InterventionCategory Mod = InterventionCategory.Modalities;
    private const InterventionCategory Dn = InterventionCategory.DryNeedling;
    private const InterventionCategory Edu = InterventionCategory.PatientEducation;
    private const InterventionCategory Hep = InterventionCategory.HomeExerciseProgram;

    public sealed record Item(string Code, string Name, InterventionCategory Category, string? Cpt, bool Timed, string? Region = null,
        int? Sets = null, int? Reps = null, string? Resistance = null, string? Duration = null, string? Equipment = null, string? Position = null);

    public static IReadOnlyList<Item> All { get; } =
    [
        // Therapeutic exercise (97110)
        new("quad-sets", "Quad sets", Ex, "97110", true, "Knee", 3, 10, Duration: "5 sec hold", Position: "Supine"),
        new("straight-leg-raise", "Straight leg raise", Ex, "97110", true, "Hip/knee", 3, 10, Position: "Supine"),
        new("short-arc-quad", "Short arc quad", Ex, "97110", true, "Knee", 3, 10, Equipment: "Bolster"),
        new("bridges", "Bridges", Ex, "97110", true, "Hip", 3, 10, Position: "Supine"),
        new("clamshells", "Clamshells", Ex, "97110", true, "Hip", 3, 12, "Yellow band", Position: "Sidelying"),
        new("mini-squats", "Mini squats", Ex, "97110", true, "Knee", 3, 10, Position: "Standing"),
        new("heel-raises", "Heel raises", Ex, "97110", true, "Ankle", 3, 15, Position: "Standing"),
        new("leg-press", "Leg press", Ex, "97110", true, "Knee", 3, 10, "40 lb", Equipment: "Leg press machine"),
        new("stationary-bike", "Stationary bike", Ex, "97110", true, "Lower extremity", Duration: "8 min", Equipment: "Recumbent bike"),
        new("shoulder-er-band", "Shoulder external rotation with band", Ex, "97110", true, "Shoulder", 3, 12, "Red band"),
        new("scapular-retraction", "Scapular retraction", Ex, "97110", true, "Shoulder", 3, 12, "Red band"),
        new("chin-tucks", "Chin tucks", Ex, "97110", true, "Cervical", 2, 10, Duration: "5 sec hold"),
        new("hamstring-stretch", "Hamstring stretch", Ex, "97110", true, "Hip/knee", 3, Duration: "30 sec hold"),
        new("core-stabilization", "Core stabilization", Ex, "97110", true, "Lumbar", 3, 10),
        // Therapeutic activity (97530)
        new("sit-to-stand-training", "Sit-to-stand training", Act, "97530", true, "Functional", 3, 10),
        new("step-ups", "Step-ups", Act, "97530", true, "Knee", 3, 10, Equipment: "6 in step"),
        new("lifting-mechanics", "Lifting mechanics training", Act, "97530", true, "Lumbar", 2, 8, "10 lb box"),
        new("transfer-training", "Transfer training", Act, "97530", true, "Functional"),
        new("agility-ladder", "Agility drills", Act, "97530", true, "Lower extremity", Equipment: "Agility ladder"),
        // Neuromuscular re-education (97112)
        new("single-leg-balance", "Single-leg balance", Nmr, "97112", true, "Balance", 3, Duration: "30 sec"),
        new("tandem-stance", "Tandem stance", Nmr, "97112", true, "Balance", 3, Duration: "30 sec"),
        new("foam-balance", "Balance on foam", Nmr, "97112", true, "Balance", 3, Duration: "30 sec", Equipment: "Foam pad"),
        new("proprioceptive-training", "Proprioceptive training", Nmr, "97112", true, "Lower extremity", Equipment: "BOSU"),
        new("gaze-stabilization", "Gaze stabilization (VOR x1)", Nmr, "97112", true, "Vestibular", 3, Duration: "60 sec"),
        new("pelvic-floor-training", "Pelvic floor muscle training", Nmr, "97112", true, "Pelvic floor", 3, 10),
        // Manual therapy (97140)
        new("joint-mobilization", "Joint mobilization", Man, "97140", true),
        new("soft-tissue-mobilization", "Soft tissue mobilization", Man, "97140", true),
        new("patellar-mobilization", "Patellar mobilization", Man, "97140", true, "Knee"),
        new("manual-stretching", "Manual stretching", Man, "97140", true),
        new("myofascial-release", "Myofascial release", Man, "97140", true),
        new("manual-traction", "Manual traction", Man, "97140", true, "Cervical"),
        // Gait training (97116)
        new("gait-training", "Gait training", Gait, "97116", true, "Gait"),
        new("stair-training", "Stair training", Gait, "97116", true, "Gait", Equipment: "Stairs with rails"),
        new("assistive-device-training", "Assistive device training", Gait, "97116", true, "Gait"),
        // Self-care / home management (97535)
        new("adl-training", "ADL training", Self, "97535", true, "Functional"),
        new("home-safety-training", "Home safety training", Self, "97535", true, "Functional"),
        // Canalith repositioning (95992, per day)
        new("epley", "Epley maneuver", Crp, "95992", false, "Vestibular"),
        new("semont", "Semont maneuver", Crp, "95992", false, "Vestibular"),
        new("bbq-roll", "BBQ (Lempert) roll", Crp, "95992", false, "Vestibular"),
        // Modalities
        new("hot-pack", "Hot pack", Mod, "97010", false, Duration: "10 min"),
        new("cold-pack", "Cold pack", Mod, "97010", false, Duration: "10 min"),
        new("estim-unattended", "Electrical stimulation, unattended", Mod, "97014", false, Duration: "15 min"),
        new("estim-attended", "Electrical stimulation, attended", Mod, "97032", true, Duration: "15 min"),
        new("ultrasound", "Therapeutic ultrasound", Mod, "97035", true, Duration: "8 min"),
        new("mechanical-traction", "Mechanical traction", Mod, "97012", false, "Lumbar"),
        // Dry needling
        new("dry-needling-1-2", "Dry needling, 1–2 muscles", Dn, "20560", false),
        new("dry-needling-3-plus", "Dry needling, 3 or more muscles", Dn, "20561", false),
        // Patient education and home program
        new("patient-education", "Patient education", Edu, null, true),
        new("posture-education", "Posture and body mechanics education", Edu, null, true),
        new("hep-instruction", "Home exercise program instruction", Hep, "97110", true),
        new("hep-review", "Home exercise program review and progression", Hep, "97110", true),
    ];

    public static async Task SeedAsync(PhysioTracDbContext db, CancellationToken ct = default)
    {
        var existing = await db.InterventionLibraryItems.Where(i => i.IsSystem).ToDictionaryAsync(i => i.Code, ct);
        foreach (var d in All)
        {
            if (!existing.TryGetValue(d.Code, out var row))
            {
                row = new InterventionLibraryItem { Code = d.Code, IsSystem = true };
                db.InterventionLibraryItems.Add(row);
            }
            row.Name = d.Name;
            row.Category = d.Category;
            row.CptCode = d.Cpt;
            row.IsTimed = d.Timed;
            row.BodyRegion = d.Region;
            row.DefaultSets = d.Sets;
            row.DefaultRepetitions = d.Reps;
            row.DefaultResistance = d.Resistance;
            row.DefaultDuration = d.Duration;
            row.DefaultEquipment = d.Equipment;
            row.DefaultPosition = d.Position;
        }
        await db.SaveChangesAsync(ct);
    }
}

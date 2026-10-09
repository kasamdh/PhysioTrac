using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using R = PhysioTrac.Domain.Enums.ExerciseBodyRegion;
using C = PhysioTrac.Domain.Enums.ExerciseCategory;
using D = PhysioTrac.Domain.Enums.ExerciseDifficulty;
using P = PhysioTrac.Domain.Enums.ExercisePosition;
using L = PhysioTrac.Domain.Enums.ExerciseLaterality;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>Starter platform exercises for the Home Exercise Program
/// library: common, widely taught exercises with plain-language
/// instructions. Every one is marked NeedsClinicalReview -- a clinician
/// confirms or edits the wording before relying on it -- and none has an
/// image: approved, properly licensed illustrations are uploaded later.
/// Contraindications are left for clinicians to complete. Idempotent:
/// adds only exercises whose code isn't there yet.</summary>
public static class ExerciseLibrarySeeder
{
    private const string StopIf = "Stop and contact your therapist if you feel sharp pain, numbness, tingling, dizziness or pain that lasts after the exercise.";

    private sealed record Ex(string Code, string Name, R Region, C Category, D Difficulty, P Position, L Side, string? Equipment,
        string Muscles, string Description, string Purpose, string Start, string[] Steps, string End, string? Mistakes,
        string? Precautions, string? Progression, string? Regression, string? Breathing = "Breathe normally; don't hold your breath.");

    private static readonly Ex[] Exercises =
    [
        // Neck
        new("chin-tuck", "Chin tucks", R.NeckCervical, C.NeckCervicalSpine, D.Beginner, P.Sitting, L.NotApplicable, null,
            "Deep neck flexors", "Gently draw your chin straight back, making a 'double chin'.", "Improve neck posture and deep neck muscle control.",
            "Sit tall with your shoulders relaxed and eyes looking forward.",
            ["Keep your eyes level.", "Slide your chin straight back without tipping your head down.", "Hold, then relax back to the start."],
            "Head back in a relaxed, neutral position.", "Tipping the head down instead of sliding it back; shrugging the shoulders.",
            StopIf, "Do it lying on your back with your head on a folded towel, then standing against a wall.", "Make the movement smaller."),
        new("upper-trapezius-stretch", "Upper trapezius stretch", R.NeckCervical, C.StretchingFlexibility, D.Beginner, P.Sitting, L.EitherSide, null,
            "Upper trapezius", "A gentle stretch along the side of the neck.", "Reduce neck and shoulder tension.",
            "Sit tall. Hold the edge of the chair with the hand on the side you are stretching.",
            ["Tilt your ear toward the opposite shoulder.", "Stop when you feel a gentle stretch.", "Hold, then slowly return to upright."],
            "Head upright.", "Pulling hard on the head; letting the shoulder rise.", StopIf, "Turn your nose slightly toward your armpit to stretch the back of the neck.", "Tilt less far."),
        new("cervical-rotation", "Neck rotation", R.NeckCervical, C.RangeOfMotion, D.Beginner, P.Sitting, L.Bilateral, null,
            "Neck rotators", "Turn your head slowly to look over each shoulder.", "Keep or improve neck movement.",
            "Sit tall with shoulders relaxed.", ["Slowly turn your head to one side as far as is comfortable.", "Return to the middle.", "Repeat to the other side."],
            "Facing forward.", "Moving quickly; turning the shoulders with the head.", StopIf, null, "Turn a smaller distance."),
        // Shoulder
        new("pendulum", "Pendulum swings", R.Shoulder, C.RangeOfMotion, D.Beginner, P.Standing, L.EitherSide, "Table or counter for support",
            "Shoulder (passive movement)", "Let your arm hang and swing gently in small circles.", "Gentle shoulder movement and pain relief.",
            "Stand leaning forward with your good hand on a table and the other arm hanging straight down.",
            ["Gently rock your body so the hanging arm swings forward and back.", "Then side to side.", "Then in small circles each way."],
            "Stand up slowly.", "Lifting the arm with the shoulder muscles instead of letting it swing.", StopIf, "Make the circles slightly larger.", "Make the swings smaller."),
        new("scapular-retraction", "Shoulder blade squeezes", R.Shoulder, C.PostureErgonomics, D.Beginner, P.Sitting, L.Bilateral, null,
            "Middle trapezius, rhomboids", "Squeeze your shoulder blades back and down.", "Improve posture and shoulder blade control.",
            "Sit or stand tall with arms by your sides.", ["Gently squeeze your shoulder blades back and down.", "Hold.", "Relax."],
            "Shoulders relaxed.", "Shrugging up toward the ears; arching the low back.", StopIf, "Add a resistance band row.", "Squeeze more gently."),
        new("wall-slide", "Wall slides", R.Shoulder, C.RangeOfMotion, D.Beginner, P.Standing, L.Bilateral, "Wall",
            "Shoulder flexors, serratus anterior", "Slide your hands up a wall as high as is comfortable.", "Improve overhead shoulder movement.",
            "Stand facing a wall with your forearms resting on it at shoulder height.", ["Slide your hands up the wall.", "Go only as high as is comfortable.", "Slide back down slowly."],
            "Forearms at shoulder height.", "Shrugging; arching the back.", StopIf, "Lift your hands slightly off the wall at the top.", "Slide a shorter distance."),
        new("band-external-rotation", "External rotation with band", R.Shoulder, C.Strengthening, D.Intermediate, P.Standing, L.EitherSide, "Resistance band, small towel",
            "Infraspinatus, teres minor", "Rotate your forearm outward against a band, elbow at your side.", "Strengthen the rotator cuff.",
            "Stand with the band anchored at elbow height. Elbow bent to 90 degrees, a small rolled towel between elbow and side.",
            ["Keeping your elbow at your side, rotate your forearm outward.", "Pause.", "Slowly return."],
            "Forearm across your stomach.", "Letting the elbow drift away from the body.", StopIf, "Use a stronger band.", "Do it without a band."),
        new("band-row", "Rows with band", R.Shoulder, C.Strengthening, D.Intermediate, P.Standing, L.Bilateral, "Resistance band",
            "Rhomboids, middle trapezius, latissimus dorsi", "Pull a band toward you, squeezing your shoulder blades.", "Strengthen upper back muscles.",
            "Stand facing the band anchored at chest height, holding an end in each hand, arms straight.",
            ["Pull your elbows back past your sides.", "Squeeze your shoulder blades together.", "Slowly return."],
            "Arms straight in front.", "Shrugging; leaning back.", StopIf, "Use a stronger band.", "Use a lighter band."),
        new("doorway-pec-stretch", "Doorway chest stretch", R.Shoulder, C.StretchingFlexibility, D.Beginner, P.Standing, L.Bilateral, "Doorway",
            "Pectoralis major and minor", "Lean gently through a doorway to stretch the chest.", "Improve posture and chest flexibility.",
            "Stand in a doorway with forearms on the frame, elbows at shoulder height.", ["Step one foot forward.", "Lean gently until you feel a stretch across the chest.", "Hold."],
            "Step back out.", "Leaning too far; arching the low back.", StopIf, null, "Stretch one side at a time."),
        new("wall-push-up", "Wall push-ups", R.Shoulder, C.Strengthening, D.Beginner, P.Standing, L.Bilateral, "Wall",
            "Pectoralis, triceps, serratus anterior", "A push-up against a wall.", "Strengthen the chest and arms.",
            "Stand an arm's length from a wall, hands on the wall at shoulder height.", ["Bend your elbows to bring your chest toward the wall.", "Push back to the start."],
            "Arms straight.", "Letting the hips sag; flaring the elbows wide.", StopIf, "Step farther from the wall, or use a counter.", "Stand closer to the wall."),
        // Elbow, wrist, hand
        new("wrist-flexor-stretch", "Wrist flexor stretch", R.ElbowWristHand, C.StretchingFlexibility, D.Beginner, P.Sitting, L.EitherSide, null,
            "Wrist and finger flexors", "Stretch the palm side of the forearm.", "Improve wrist flexibility.",
            "Hold your arm out in front, elbow straight, palm up.", ["With the other hand, gently bend the wrist back.", "Hold."],
            "Release.", "Bending the elbow; pulling hard.", StopIf, null, "Bend the elbow slightly."),
        new("wrist-extensor-stretch", "Wrist extensor stretch", R.ElbowWristHand, C.StretchingFlexibility, D.Beginner, P.Sitting, L.EitherSide, null,
            "Wrist extensors", "Stretch the back of the forearm.", "Improve wrist flexibility.",
            "Hold your arm out in front, elbow straight, palm down.", ["With the other hand, gently bend the wrist down.", "Hold."],
            "Release.", "Bending the elbow; pulling hard.", StopIf, null, "Bend the elbow slightly."),
        new("tendon-glides", "Tendon glides", R.ElbowWristHand, C.RangeOfMotion, D.Beginner, P.Sitting, L.EitherSide, null,
            "Finger flexor tendons", "Move your fingers through a set of positions.", "Keep the finger tendons gliding.",
            "Hold your hand up, fingers straight.", ["Make a hook fist.", "Straighten.", "Make a full fist.", "Straighten.", "Make a straight fist (bend at the knuckles only)."],
            "Fingers straight.", "Rushing through the positions.", StopIf, null, null),
        // Thoracic
        new("thoracic-extension-chair", "Upper back extension over chair", R.ThoracicSpine, C.RangeOfMotion, D.Beginner, P.Sitting, L.NotApplicable, "Chair with a firm back",
            "Thoracic extensors", "Gently lean back over the top of the chair.", "Improve upper back movement.",
            "Sit in a firm chair with hands behind your head, elbows forward.", ["Gently lean your upper back over the top of the chair.", "Return to upright."],
            "Sitting upright.", "Arching the low back instead of the upper back.", StopIf, null, "Make the movement smaller."),
        new("open-book", "Open book rotation", R.ThoracicSpine, C.RangeOfMotion, D.Beginner, P.SideLying, L.EitherSide, null,
            "Thoracic rotators", "Lying on your side, open your top arm like a book.", "Improve upper back rotation.",
            "Lie on your side, knees bent, arms straight out in front together.", ["Lift the top arm and open it across your body, following it with your eyes.", "Return."],
            "Arms together.", "Letting the knees separate.", StopIf, null, "Open a smaller distance."),
        // Low back
        new("pelvic-tilt", "Pelvic tilts", R.LowBack, C.CoreStrengthening, D.Beginner, P.Supine, L.NotApplicable, null,
            "Abdominals", "Gently flatten your low back into the floor.", "Core control and low back movement.",
            "Lie on your back, knees bent, feet flat.", ["Tighten your stomach muscles and flatten your low back.", "Hold.", "Relax."],
            "Back relaxed.", "Holding your breath; pushing with the legs.", StopIf, null, null),
        new("knee-to-chest", "Single knee to chest", R.LowBack, C.StretchingFlexibility, D.Beginner, P.Supine, L.EitherSide, null,
            "Low back extensors, gluteals", "Hug one knee toward your chest.", "Relieve low back stiffness.",
            "Lie on your back, knees bent.", ["Bring one knee toward your chest and hold it with your hands.", "Hold.", "Lower slowly."],
            "Both feet on the floor.", "Lifting the head.", StopIf, "Bring both knees up together.", "Hold behind the thigh."),
        new("cat-camel", "Cat and camel", R.LowBack, C.RangeOfMotion, D.Beginner, P.Kneeling, L.NotApplicable, null,
            "Spinal extensors and flexors", "On hands and knees, slowly arch and round your back.", "Spinal movement.",
            "On hands and knees, hands under shoulders and knees under hips.", ["Round your back up toward the ceiling.", "Then let it gently sag, lifting your head.", "Move slowly."],
            "Back flat.", "Moving fast; going into pain.", StopIf, null, "Make the movement smaller."),
        new("prone-press-up", "Prone press-ups", R.LowBack, C.RangeOfMotion, D.Beginner, P.Prone, L.NotApplicable, null,
            "Lumbar extensors", "Lying face down, push your upper body up on your hands.", "Low back extension.",
            "Lie on your stomach with hands under your shoulders.", ["Push your upper body up, keeping your hips on the floor.", "Hold.", "Lower slowly."],
            "Lying flat.", "Lifting the hips; tensing the buttocks.", StopIf + " Stop if the pain spreads further down the leg.", null, "Prop up on your elbows instead."),
        new("bird-dog", "Bird dog", R.LowBack, C.CoreStrengthening, D.Intermediate, P.Kneeling, L.Bilateral, null,
            "Multifidus, gluteals, abdominals", "On hands and knees, reach one arm and the opposite leg out.", "Core and back stability.",
            "On hands and knees, back flat.", ["Tighten your stomach.", "Reach one arm forward and the opposite leg back.", "Hold, then switch sides."],
            "Hands and knees.", "Arching the back; rotating the hips.", StopIf, null, "Move only the arm, or only the leg."),
        new("dead-bug", "Dead bug", R.Core, C.CoreStrengthening, D.Intermediate, P.Supine, L.Bilateral, null,
            "Transversus abdominis, rectus abdominis", "On your back, lower the opposite arm and leg slowly.", "Core control.",
            "Lie on your back, arms up, hips and knees bent to 90 degrees.", ["Keep your low back flat.", "Slowly lower one arm and the opposite leg.", "Return and switch."],
            "Arms up, knees up.", "Back arching off the floor.", StopIf, null, "Move only the legs."),
        new("forearm-plank", "Forearm plank", R.Core, C.CoreStrengthening, D.Intermediate, P.Prone, L.NotApplicable, null,
            "Abdominals, shoulder stabilizers", "Hold your body straight on forearms and toes.", "Core endurance.",
            "Lie face down, elbows under shoulders.", ["Lift your body onto forearms and toes.", "Keep a straight line from head to heels.", "Hold."],
            "Lower down.", "Sagging or raising the hips; holding your breath.", StopIf, null, "Rest on your knees instead of your toes."),
        new("side-plank-knees", "Side plank on knees", R.Core, C.CoreStrengthening, D.Intermediate, P.SideLying, L.EitherSide, null,
            "Obliques, quadratus lumborum, gluteus medius", "Lift your hips while lying on your side.", "Side core strength.",
            "Lie on your side, elbow under shoulder, knees bent.", ["Lift your hips so your body is straight from head to knees.", "Hold.", "Lower."],
            "Lying on your side.", "Hips sagging or rolling.", StopIf, "Straighten the legs.", null),
        new("diaphragmatic-breathing", "Belly breathing", R.Core, C.NeuromuscularReeducation, D.Beginner, P.Supine, L.NotApplicable, null,
            "Diaphragm", "Slow breathing into your belly.", "Relaxation and breathing control.",
            "Lie on your back with knees bent, one hand on your chest and one on your belly.",
            ["Breathe in slowly through your nose so the belly hand rises.", "Breathe out slowly through pursed lips."],
            "Relaxed.", "Lifting the chest and shoulders.", "Stop if you feel light-headed.", "Do it sitting, then standing.", null, null),
        // Hip
        new("glute-bridge", "Bridges", R.Hip, C.Strengthening, D.Beginner, P.Supine, L.Bilateral, null,
            "Gluteus maximus, hamstrings", "Lift your hips off the floor.", "Strengthen the hips and buttocks.",
            "Lie on your back, knees bent, feet flat and hip-width apart.", ["Tighten your buttocks.", "Lift your hips until your body is straight from shoulders to knees.", "Lower slowly."],
            "Lying flat.", "Arching the low back; pushing through the toes.", StopIf, "Single-leg bridge.", "Lift a smaller distance."),
        new("clamshell", "Clamshells", R.Hip, C.Strengthening, D.Beginner, P.SideLying, L.EitherSide, "Resistance band (optional)",
            "Gluteus medius", "On your side, open your top knee like a clamshell.", "Strengthen the outer hip.",
            "Lie on your side, hips and knees bent, feet together.", ["Keeping your feet together, lift the top knee.", "Don't roll your hips back.", "Lower slowly."],
            "Knees together.", "Rolling the pelvis backward.", StopIf, "Add a band around the thighs.", null),
        new("side-lying-hip-abduction", "Side-lying leg raise", R.Hip, C.Strengthening, D.Beginner, P.SideLying, L.EitherSide, null,
            "Gluteus medius", "On your side, lift your top leg.", "Strengthen the outer hip.",
            "Lie on your side, bottom knee bent, top leg straight.", ["Lift the top leg slightly behind you, toes forward.", "Lower slowly."],
            "Leg down.", "Rolling the hips back; leading with the toes up.", StopIf, "Add an ankle weight.", "Lift a smaller distance."),
        new("hip-flexor-stretch", "Hip flexor stretch", R.Hip, C.StretchingFlexibility, D.Beginner, P.Kneeling, L.EitherSide, "Cushion for the knee",
            "Iliopsoas, rectus femoris", "In a half-kneeling position, shift forward to stretch the front of the hip.", "Hip flexibility.",
            "Kneel on one knee (on a cushion), the other foot flat in front.", ["Tuck your pelvis under.", "Shift forward until you feel a stretch at the front of the hip.", "Hold."],
            "Upright kneeling.", "Arching the low back.", StopIf, null, "Do it standing in a lunge."),
        new("piriformis-stretch", "Piriformis stretch", R.Hip, C.StretchingFlexibility, D.Beginner, P.Supine, L.EitherSide, null,
            "Piriformis, hip rotators", "Cross one ankle over the other knee and pull gently.", "Buttock and hip flexibility.",
            "Lie on your back, knees bent. Cross one ankle over the other knee.", ["Hold behind the lower thigh.", "Gently pull it toward your chest.", "Hold."],
            "Both feet down.", "Lifting the head; forcing the stretch.", StopIf, null, "Do it sitting in a chair."),
        new("hamstring-stretch", "Hamstring stretch", R.Hip, C.StretchingFlexibility, D.Beginner, P.Supine, L.EitherSide, "Strap or towel",
            "Hamstrings", "Raise one straight leg with a strap.", "Hamstring flexibility.",
            "Lie on your back with a strap around one foot.", ["Raise the straight leg until you feel a stretch behind the thigh.", "Hold."],
            "Leg down.", "Bending the knee; lifting the hips.", StopIf, null, "Bend the other knee."),
        // Knee
        new("quad-set", "Quad sets", R.Knee, C.Strengthening, D.Beginner, P.Supine, L.EitherSide, "Small towel",
            "Quadriceps", "Tighten the thigh muscle to push the knee down.", "Activate the thigh muscle.",
            "Sit or lie with your leg straight and a small rolled towel under the knee.", ["Tighten your thigh muscle, pressing the knee into the towel.", "Hold.", "Relax."],
            "Relaxed.", "Holding your breath; lifting the heel high.", StopIf, "Straight leg raise.", null),
        new("straight-leg-raise", "Straight leg raise", R.Knee, C.Strengthening, D.Beginner, P.Supine, L.EitherSide, null,
            "Quadriceps, hip flexors", "Lift a straight leg off the bed.", "Strengthen the thigh.",
            "Lie on your back, one knee bent, the other leg straight.", ["Tighten the thigh of the straight leg.", "Lift it to the height of the other knee.", "Lower slowly."],
            "Leg down.", "Letting the knee bend.", StopIf, "Add an ankle weight.", "Quad sets."),
        new("heel-slide", "Heel slides", R.Knee, C.RangeOfMotion, D.Beginner, P.Supine, L.EitherSide, null,
            "Knee flexion", "Slide your heel toward your buttocks.", "Improve knee bending.",
            "Lie on your back, legs straight.", ["Slide one heel toward your buttocks, bending the knee.", "Slide it back out."],
            "Legs straight.", "Moving fast.", StopIf, null, "Use a strap to help."),
        new("short-arc-quad", "Short arc quads", R.Knee, C.Strengthening, D.Beginner, P.Supine, L.EitherSide, "Rolled towel or foam roll",
            "Quadriceps", "Straighten the knee over a roll.", "Strengthen the end of knee straightening.",
            "Lie with a roll under the knee.", ["Straighten the knee, lifting the heel.", "Hold.", "Lower slowly."],
            "Knee resting over the roll.", "Lifting the thigh off the roll.", StopIf, "Add an ankle weight.", null),
        new("mini-squat", "Mini squats", R.Knee, C.Strengthening, D.Intermediate, P.Standing, L.Bilateral, "Counter for balance",
            "Quadriceps, gluteals", "A small, controlled squat holding a counter.", "Leg strength for daily activities.",
            "Stand with feet hip-width apart, holding a counter.", ["Bend your knees a small amount, as if starting to sit.", "Keep your knees over your toes.", "Stand back up."],
            "Standing tall.", "Knees moving inward; heels lifting.", StopIf, "Squat deeper, or let go of the counter.", "Bend less."),
        new("sit-to-stand", "Sit to stand", R.Knee, C.Strengthening, D.Beginner, P.Sitting, L.Bilateral, "Sturdy chair",
            "Quadriceps, gluteals", "Stand up from a chair and sit back down.", "Leg strength and independence.",
            "Sit near the front of a sturdy chair, feet flat.", ["Lean forward.", "Stand up fully.", "Sit back down slowly and with control."],
            "Seated.", "Dropping into the chair.", StopIf, "Cross your arms; use a lower chair.", "Use your hands on the armrests."),
        new("step-up", "Step-ups", R.Knee, C.Strengthening, D.Intermediate, P.Standing, L.EitherSide, "Step, rail",
            "Quadriceps, gluteals", "Step up onto a step and back down.", "Strength for stairs.",
            "Stand facing a low step, holding a rail.", ["Step up with one foot.", "Bring the other foot up.", "Step down with control."],
            "Both feet on the floor.", "Pushing off the bottom foot; knee caving in.", StopIf, "Use a higher step.", "Use a lower step."),
        new("terminal-knee-extension", "Terminal knee extension with band", R.Knee, C.Strengthening, D.Intermediate, P.Standing, L.EitherSide, "Resistance band",
            "Quadriceps", "Straighten the knee against a band behind it.", "Strengthen full knee straightening.",
            "Stand with a band anchored in front and looped behind one knee, knee slightly bent.", ["Straighten the knee against the band.", "Hold.", "Slowly bend again."],
            "Knee slightly bent.", "Leaning the body back.", StopIf, "Stronger band.", "Lighter band."),
        // Ankle and foot
        new("ankle-pumps", "Ankle pumps", R.AnkleFoot, C.RangeOfMotion, D.Beginner, P.Supine, L.Bilateral, null,
            "Ankle dorsiflexors and plantarflexors", "Point and flex your feet.", "Ankle movement and circulation.",
            "Lie or sit with your legs out.", ["Point your toes away.", "Pull them back toward you."], "Relaxed.", null, StopIf, null, null),
        new("ankle-alphabet", "Ankle alphabet", R.AnkleFoot, C.RangeOfMotion, D.Beginner, P.Sitting, L.EitherSide, null,
            "Ankle muscles", "Trace the letters of the alphabet with your foot.", "Ankle movement in all directions.",
            "Sit with the foot off the floor.", ["Using your ankle, not your knee, trace each letter A to Z."], "Foot down.", "Moving the whole leg.", StopIf, null, "Fewer letters."),
        new("heel-raise", "Heel raises", R.AnkleFoot, C.Strengthening, D.Beginner, P.Standing, L.Bilateral, "Counter for balance",
            "Gastrocnemius, soleus", "Rise up onto your toes.", "Calf strength.",
            "Stand holding a counter, feet hip-width apart.", ["Rise up onto your toes.", "Lower slowly."],
            "Feet flat.", "Rolling to the outside of the foot.", StopIf, "Single-leg heel raises.", "Do it seated."),
        new("calf-stretch-wall", "Calf stretch at a wall", R.AnkleFoot, C.StretchingFlexibility, D.Beginner, P.Standing, L.EitherSide, "Wall",
            "Gastrocnemius", "Lean into a wall with one leg back.", "Calf flexibility.",
            "Stand facing a wall, one foot back, heel down, back knee straight.", ["Lean forward until you feel a stretch in the back calf.", "Hold."],
            "Feet together.", "Lifting the back heel.", StopIf, "Bend the back knee to stretch the lower calf.", null),
        new("band-ankle-eversion", "Ankle eversion with band", R.AnkleFoot, C.Strengthening, D.Intermediate, P.Sitting, L.EitherSide, "Resistance band",
            "Peroneals", "Turn the foot outward against a band.", "Ankle stability.",
            "Sit with a band around the forefoot, anchored to the inside.", ["Turn the foot outward against the band.", "Slowly return."],
            "Foot neutral.", "Moving the whole leg.", StopIf, "Stronger band.", "Without the band."),
        // Balance and gait
        new("tandem-stance", "Tandem stance", R.WholeBody, C.BalanceCoordination, D.Intermediate, P.Standing, L.NotApplicable, "Counter for safety",
            "Balance", "Stand heel-to-toe.", "Improve balance.",
            "Stand at a counter with one foot directly in front of the other.", ["Hold the position, touching the counter only if needed.", "Switch feet."],
            "Feet apart.", "Locking the knees.", "Always have a sturdy support within reach. " + StopIf, "Close your eyes (only with support).", "Feet slightly apart."),
        new("single-leg-stance", "Single-leg stance", R.WholeBody, C.BalanceCoordination, D.Intermediate, P.Standing, L.EitherSide, "Counter for safety",
            "Balance, hip stabilizers", "Balance on one leg.", "Balance and fall prevention.",
            "Stand at a counter.", ["Lift one foot.", "Hold your balance.", "Switch legs."],
            "Both feet down.", "Leaning the body.", "Always have a sturdy support within reach. " + StopIf, "Stand on a cushion (with support).", "Lightly touch the counter."),
        new("marching-in-place", "Standing marching", R.WholeBody, C.FallPrevention, D.Beginner, P.Standing, L.Bilateral, "Counter for support",
            "Hip flexors, balance", "March in place holding a counter.", "Balance and leg strength.",
            "Stand at a counter.", ["Lift one knee, then the other, in a slow march."], "Standing.", "Leaning back.",
            "Always have a sturdy support within reach. " + StopIf, "Let go of the counter.", "March seated."),
        new("heel-toe-walking", "Heel-to-toe walking", R.WholeBody, C.GaitTraining, D.Intermediate, P.Standing, L.NotApplicable, "Hallway with a rail or wall",
            "Balance", "Walk placing the heel directly in front of the toes.", "Balance while walking.",
            "Stand next to a wall or rail.", ["Step so the heel of one foot touches the toes of the other.", "Continue for several steps.", "Look ahead, not down."],
            "Stop and stand.", "Rushing; looking at the feet.", "Do this next to a wall or rail. " + StopIf, null, "Walk with feet slightly apart."),
        // Pelvic health
        new("pelvic-floor-contraction", "Pelvic floor contractions", R.Pelvis, C.PelvicHealth, D.Beginner, P.Supine, L.NotApplicable, null,
            "Pelvic floor muscles", "Gently squeeze and lift the pelvic floor muscles.", "Pelvic floor strength and control.",
            "Lie on your back with knees bent, or sit.", ["Squeeze the muscles you would use to stop passing gas, and lift.", "Hold.", "Fully relax."],
            "Relaxed.", "Squeezing the buttocks or thighs; holding your breath.", "Do this as your therapist showed you. " + StopIf, null, null,
            "Breathe out as you squeeze; don't hold your breath."),
    ];

    public static async Task SeedAsync(PhysioTracDbContext db, CancellationToken ct = default)
    {
        var existing = (await db.Exercises.Where(x => x.OrganizationId == null).Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        var added = false;
        foreach (var e in Exercises.Where(e => !existing.Contains(e.Code)))
        {
            db.Exercises.Add(new Exercise
            {
                Code = e.Code,
                Name = e.Name,
                BodyRegion = e.Region,
                Category = e.Category,
                Difficulty = e.Difficulty,
                Position = e.Position,
                Laterality = e.Side,
                Equipment = e.Equipment,
                TargetMuscles = e.Muscles,
                PatientDescription = e.Description,
                ClinicalPurpose = e.Purpose,
                StartingPosition = e.Start,
                Instructions = string.Join("\n", e.Steps),
                EndingPosition = e.End,
                BreathingInstructions = e.Breathing,
                CommonMistakes = e.Mistakes,
                SafetyPrecautions = e.Precautions,
                Progressions = e.Progression,
                Regressions = e.Regression,
                NeedsClinicalReview = true,
            });
            added = true;
        }
        if (added) await db.SaveChangesAsync(ct);
    }

    /// <summary>The number of starter exercises (for tests).</summary>
    public static int Count => Exercises.Length;
}

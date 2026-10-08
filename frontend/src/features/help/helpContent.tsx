import type { ReactNode } from "react";

/** Magenta "Note:" call-out, as in the help screens this mirrors. */
export function Note({ children }: { children: ReactNode }) {
  return (
    <p className="my-2 border-l-4 border-slate-200 pl-3 text-[#a3127c]">
      <strong>Note</strong>: {children}
    </p>
  );
}

/** A bold term with its explanation indented underneath. */
export function Term({ name, children }: { name: string; children: ReactNode }) {
  return (
    <div className="mb-3">
      <p className="font-bold text-[#333]">{name}</p>
      <div className="pl-7 text-[#333]">{children}</div>
    </div>
  );
}

export interface HelpSection {
  id: string;
  title: string;
  body: ReactNode;
}

export interface PageHelp {
  title: string;
  intro: ReactNode;
  sections: HelpSection[];
}

/** The navy bar at the top of every page -- the same on all of them. */
export const headerSection: HelpSection = {
  id: "header",
  title: "Header",
  body: (
    <>
      <Term name="PhysioTrac logo">The name of the app. Use Home to get back to the main screen.</Term>
      <Term name="Go Back">Returns to the previous screen.</Term>
      <Term name="Home, Patients, Schedule, Providers, Billing">
        Opens that area. The underlined one is where you are now.
        <Note>On screens narrower than a large desktop these move into the ☰ menu button.</Note>
      </Term>
      <Term name="Welcome line">
        Your name, when you last signed in (the session before this one), and the name of the current page.
      </Term>
      <Term name="Logout">Signs you out. The sign-in screen then confirms the time you logged out.</Term>
      <Term name="Print">Prints the current page.</Term>
      <Term name="? (Help)">Opens these instructions for the page you are on.</Term>
      <Note>For your security you are signed out automatically after a period of inactivity, with a warning first.</Note>
    </>
  ),
};

const listsSection: HelpSection = {
  id: "using-lists",
  title: "Using lists",
  body: (
    <>
      <Term name="Quick-filter buttons">The joined buttons above a list (for example All / Active). The filled navy one is selected.</Term>
      <Term name="Search and dropdowns">Narrow the list further. Most filters are kept in the page address, so you can bookmark or share a filtered list.</Term>
      <Term name="Refresh">Reloads the list with the latest information.</Term>
      <Term name="☰ (row menu)">The button at the start of each row opens the actions for that row.</Term>
      <Term name="Blue text">Blue names and numbers are links — click them to open the item.</Term>
      <Note>On a phone each row is shown as a card, with the field names on the left.</Note>
    </>
  ),
};

const patientColumns = (
  <Term name="Columns">
    MRN (medical record number), name, date of birth (age), phone, primary location, status, last visit and next
    appointment. Cancelled appointments are not counted as visits.
  </Term>
);

const patientFilters = (
  <>
    <Term name="Today / Yesterday / Last 3 Days / Next 7 Days">
      Shows patients with an appointment in that period. Use From / To for any other dates.
    </Term>
    <Term name="All / Active / Inactive / Discharged">Filters by the patient’s status.</Term>
    <Term name="Location">
      With dates chosen: patients with an appointment at that location in those dates. Without dates: patients whose
      primary clinic it is, or who have any appointment there.
    </Term>
    <Term name="Search">Matches first name, last name or MRN.</Term>
    <Term name="Clear filters">Removes every filter.</Term>
  </>
);

export const pageHelp: Record<string, PageHelp> = {
  home: {
    title: "Home",
    intro: (
      <>
        <p>The main screen. It shows your organization, the clinic location you are working at, and a button for each area of PhysioTrac you can use.</p>
        <Note>You only see the buttons your access level allows.</Note>
      </>
    ),
    sections: [
      {
        id: "location",
        title: "Location",
        body: (
          <Term name="Location dropdown">
            Choose the clinic you are working at. It is remembered on this device and is used as the starting
            location, for example when adding a patient.
          </Term>
        ),
      },
      {
        id: "buttons",
        title: "Buttons",
        body: (
          <>
            <Term name="Administration">Change your password, messages, users, locations and the patient list (add / edit / delete patients).</Term>
            <Term name="Billing">Billing (coming soon).</Term>
            <Term name="Patients">The list of patients, with filters.</Term>
            <Term name="Provider Hours">Each provider’s working hours, time off and blocked time.</Term>
            <Term name="Providers">The list of all providers.</Term>
            <Term name="Schedule">The appointment calendar.</Term>
            <Term name="Workflow">Today’s appointments for one therapist: check patients in and document visits in one place.</Term>
          </>
        ),
      },
    ],
  },

  patients: {
    title: "Patients",
    intro: (
      <>
        <p>The list of patients in your organization, with filters to find who you need.</p>
        <Note>This list is read-only. Patients are added, edited and deleted in Administration › Patient List.</Note>
      </>
    ),
    sections: [
      { id: "filters", title: "Filters", body: patientFilters },
      {
        id: "list",
        title: "The list",
        body: (
          <>
            {patientColumns}
            <Term name="MRN (blue)">Opens that patient’s appointments in the Schedule.</Term>
            <Term name="☰ menu">View appointments, or Send message to the patient.</Term>
          </>
        ),
      },
      listsSection,
    ],
  },

  "admin-patients": {
    title: "Patient List",
    intro: (
      <>
        <p>Administration’s patient list: everything on the Patients page, plus adding, editing, deleting and restoring patients.</p>
        <Note>Deleting a patient hides the chart; it is never erased and can be restored.</Note>
      </>
    ),
    sections: [
      {
        id: "add",
        title: "Add patient",
        body: (
          <>
            <Term name="+ Add patient">
              Opens the Add Patient window with all of the patient’s details. Patient name and date of birth are required —
              they are marked in red with “Required” until filled. Primary location starts as the clinic chosen on Home.
            </Term>
            <Term name="Save and Close">Becomes available once the required fields are filled.</Term>
            <Term name="Possible duplicate">
              If a patient with the same name and date of birth already exists, it is shown first. Use the existing chart,
              or choose Add anyway if this really is a different person.
            </Term>
            <Note>After saving, a banner shows the new patient’s MRN.</Note>
          </>
        ),
      },
      {
        id: "edit",
        title: "Edit patient",
        body: (
          <>
            <Term name="Open">Click the patient’s name, or choose Edit patient from the ☰ menu.</Term>
            <Term name="Fields">
              Name, date of birth, status (Active / Inactive / Discharged), phone, e-mail, address, emergency contact,
              language, primary location and assigned therapist.
            </Term>
            <Term name="Save and Close">Becomes available once you change something.</Term>
          </>
        ),
      },
      {
        id: "delete",
        title: "Delete and restore",
        body: (
          <>
            <Term name="Delete patient">From the ☰ menu. After you confirm, the patient disappears from every list.</Term>
            <Term name="Deleted">Shows deleted patients. Use Restore patient in the ☰ menu to bring one back.</Term>
          </>
        ),
      },
      { id: "filters", title: "Filters", body: patientFilters },
      { id: "list", title: "The list", body: patientColumns },
      listsSection,
    ],
  },

  schedule: {
    title: "Schedule",
    intro: (
      <>
        <p>The appointment calendar for every provider and location: book, move, check in and complete visits.</p>
        <Note>Appointments are colored by visit type; the legend above the calendar shows the colors. Cancelled and no-show appointments are grey and crossed out.</Note>
      </>
    ),
    sections: [
      {
        id: "views",
        title: "Views and dates",
        body: (
          <>
            <Term name="Day">One column per provider (PT / PTA) with their hours and appointment count. Click a provider’s name to see only their schedule.</Term>
            <Term name="Week / Month / Year">A wider view. Year shows how busy each day is.</Term>
            <Term name="List">The appointments as a list, for a day, week or month. Phones start in List.</Term>
            <Term name="Today / ‹ Previous / Next ›">Move through dates, or pick a date in the date box.</Term>
          </>
        ),
      },
      {
        id: "filters",
        title: "Filters",
        body: <Term name="Location, provider, status, appointment type, search">Narrow what is shown. Search matches patient name or MRN.</Term>,
      },
      {
        id: "book",
        title: "Booking an appointment",
        body: (
          <>
            <Term name="+ New appointment">Or click an empty time in Day or Week view to start with that time filled in.</Term>
            <Term name="Patient">Search by name, MRN, phone or date of birth. If they are new, create them right there.</Term>
            <Term name="Repeat">Book a series (weekly, 2× or 3× a week, or chosen days) until an end date. A preview shows every date first.</Term>
            <Note>If the time clashes with another booking, the provider’s hours, time off or a closed location, you are told why. Some conflicts can be overridden with a reason.</Note>
          </>
        ),
      },
      {
        id: "manage",
        title: "Changing an appointment",
        body: (
          <>
            <Term name="Click an appointment">Opens its details with the next steps.</Term>
            <Term name="Status steps">Confirm → Check in → Start visit → Complete. Also No show and Cancel appointment.</Term>
            <Term name="Reschedule">Pick a new date, time or provider.</Term>
            <Term name="Drag and drop">In Day and Week view, drag an appointment to move it, or drag its bottom edge to change its length. The move is checked before it is saved.</Term>
          </>
        ),
      },
      {
        id: "hours",
        title: "Provider hours",
        body: <Term name="Provider hours">Opens working hours and time off for providers (also on Home).</Term>,
      },
    ],
  },

  "provider-hours": {
    title: "Provider Hours",
    intro: <p>Each provider’s regular working hours and their time off. The Schedule shades time outside these hours and checks bookings against them.</p>,
    sections: [
      { id: "provider", title: "Choosing a provider", body: <Term name="Provider dropdown">Pick whose hours to see or change.</Term> },
      {
        id: "weekly",
        title: "Weekly hours",
        body: (
          <>
            <Term name="Each day">Start and end time at a location. Use + Add hours for a split day (for example two locations).</Term>
          </>
        ),
      },
      {
        id: "time-off",
        title: "Time off, lunch and blocked time",
        body: (
          <>
            <Term name="Add">Enter the dates or times and a reason.</Term>
            <Term name="Existing appointments">Appointments that fall inside new time off are listed so staff can reschedule them; they are not moved automatically.</Term>
          </>
        ),
      },
    ],
  },

  providers: {
    title: "Providers",
    intro: <p>Every provider (PT, PTA and others) in your organization, with where they work and whether they can sign in.</p>,
    sections: [
      {
        id: "filters",
        title: "Filters",
        body: (
          <>
            <Term name="All / PT / PTA / Other">Filter by discipline.</Term>
            <Term name="Active / Inactive / All">Inactive providers are hidden by default.</Term>
            <Term name="Location and search">Search matches name, NPI or specialty.</Term>
          </>
        ),
      },
      {
        id: "list",
        title: "The list",
        body: (
          <>
            <Term name="Name (blue)">Opens that provider’s working hours and time off.</Term>
            <Term name="☰ menu">View today’s schedule, or Working hours &amp; time off.</Term>
            <Term name="Login">Yes when the provider has their own PhysioTrac sign-in (needed to have patients assigned).</Term>
          </>
        ),
      },
      listsSection,
    ],
  },

  "documentation-dashboard": {
    title: "Documentation Dashboard",
    intro: <p>Documentation across the schedule: today’s visits and their notes, what still needs writing or signing, overdue work and upcoming deadlines. Only patients you may see are included.</p>,
    sections: [
      {
        id: "filters",
        title: "Filters",
        body: (
          <>
            <Term name="Provider">Starts on your own patients when you are a provider; choose “All providers” or another provider to see theirs.</Term>
            <Term name="Patient, note type, status">Narrow every list to one patient, one kind of note or one documentation status.</Term>
            <Term name="From / To">Dates of service. Without “From”, unsigned notes of any age are listed; visits not started look back 30 days and recently signed notes 7 days.</Term>
          </>
        ),
      },
      {
        id: "lists",
        title: "The lists",
        body: (
          <>
            <Term name="Summary tiles">The count in each list; select one to jump to it. Overdue turns red when anything is overdue.</Term>
            <Term name="Notes not started">Visits that have started with no note yet — “Start note” opens the right note for the visit. A cancelled or no-show visit offers a missed-visit note instead.</Term>
            <Term name="Ready to sign / Draft notes">Drafts with nothing missing are ready to sign; the others still have required fields or checks open.</Term>
            <Term name="Requiring cosignature / Returned for correction">An assistant’s submitted notes waiting for a PT, and notes a PT sent back (with the reason).</Term>
            <Term name="Overdue">Notes not signed by the end of the day after the visit, and visits with no note by then.</Term>
            <Term name="Deadlines">Progress notes due at your clinic’s visit count (the 10th visit by default; listed two visits early), re-evaluations whose reassessment date is within a week, and plans of care whose certification ends within 30 days.</Term>
          </>
        ),
      },
    ],
  },

  workflow: {
    title: "Workflow",
    intro: (
      <>
        <p>Today’s appointments for one therapist, so you can check patients in, document each visit and complete it without going to other screens.</p>
        <Note>Only today’s date (in your clinic’s time zone) is shown. The list refreshes itself every minute, so front-desk check-ins appear automatically.</Note>
      </>
    ),
    sections: [
      {
        id: "whose",
        title: "Whose day",
        body: (
          <>
            <Term name="Therapist">Starts on your own appointments if you have a provider schedule. Choose another therapist, or All therapists, from the dropdown.</Term>
            <Note>Depending on your access level you may only see your own day.</Note>
          </>
        ),
      },
      {
        id: "status",
        title: "Status buttons",
        body: (
          <Term name="All / To check in / Checked in / In progress / Completed / Cancelled & no-show">
            Show only appointments at that stage. The number on each button is how many there are today.
          </Term>
        ),
      },
      {
        id: "steps",
        title: "Moving a visit along",
        body: (
          <>
            <Term name="Check in">When the patient arrives (from Scheduled or Confirmed).</Term>
            <Term name="Start visit">When treatment begins (from Checked in).</Term>
            <Term name="Complete without note">In the ☰ menu, for a visit that is finished but documented elsewhere.</Term>
          </>
        ),
      },
      {
        id: "document",
        title: "Documenting a visit",
        body: (
          <>
            <Term name="Document / Continue note">
              Opens the visit in Clinical Charting (the full note screen). The first time, the visit’s draft note is created for you.
            </Term>
            <Term name="Note column">Not started, Draft, Signed, or Awaiting cosign (a PTA note waiting for a PT’s cosignature; the visit completes once it is cosigned).</Term>
            <Term name="Patient documentation">In the ☰ menu: everything documented for that patient, across all visits.</Term>
            <Term name="Open encounter (Schedule)">On the Schedule, an appointment’s details also open its encounter — the same note as Document here.</Term>
            <Term name="Your unfinished notes">Shown above the list when you have drafts from earlier visits, open amendments, or notes waiting for a cosign. Open one to finish it.</Term>
            <Term name="Waiting for your cosign">Assistants’ notes awaiting your cosignature.</Term>
          </>
        ),
      },
      listsSection,
    ],
  },

  charting: {
    title: "Clinical Charting",
    intro: (
      <>
        <p>The note for one visit: record what the patient reports, your examination findings and measurements, outcome scores, the treatment you gave and how the patient responded, progress on goals, then your assessment and plan — and sign.</p>
        <Note>Everything saves automatically as you work (“Saving… / Draft · saved 2:41 PM”). Signed notes open read-only.</Note>
        <Note>The note’s sections and fields come from its documentation template (shown in the header). Until any field is filled in, you can switch to another template — for example a specialty evaluation.</Note>
      </>
    ),
    sections: [
      {
        id: "header",
        title: "Header",
        body: (
          <>
            <Term name="Visit">The visit’s number in the current plan of care, and the visit type from the schedule.</Term>
            <Term name="Allergies / Precautions">Shown in red when the patient has any.</Term>
            <Term name="Section bar">Jumps to a section. A number shows how many required fields are still empty there; ✓ means its required fields are done.</Term>
            <Term name="▾ Section titles">Click (or press Enter on) a section title to collapse or expand it; Collapse all / Expand all does every section. The layout is remembered on this device.</Term>
            <Term name="Ctrl+S">Saves right away instead of waiting for the automatic save.</Term>
            <Term name="Saved by someone else">A yellow notice appears when another user saves the note while you have it open — reload before continuing.</Term>
            <Term name="Changed elsewhere">If someone else saved the note after you opened it, your next change isn’t saved over theirs. Reload the latest version and re-enter your change.</Term>
            <Term name="Required fields">Fields marked * must be filled in before signing; some appear only after a particular answer (for example red-flag details).</Term>
          </>
        ),
      },
      {
        id: "pain-body-chart",
        title: "Pain assessment and body chart",
        body: (
          <>
            <Term name="Pain scale">Numeric 0–10, visual analog 0–100 mm, Wong-Baker faces, or verbal (none / mild / moderate / severe). Last visit’s rating shows beside each one when the same scale was used.</Term>
            <Term name="Quality, frequency, irritability">Tap the words that apply; tap again to clear.</Term>
            <Term name="Body chart">Choose what to mark (pain, numbness, tingling, burning, swelling, tenderness, incision, scar, radiating, other), then tap the front or back drawing. R and L show the patient’s right and left. Select a marker to drag it; edit its severity, radiation, annotation and comments in the table.</Term>
            <Term name="Without the drawing">Every finding can be added and edited in the table with the keyboard (+ Add finding, then choose view, region and side).</Term>
            <Term name="Objective measurements">Range of motion (AROM/PROM, end feel, pain), strength (manual muscle test grade or dynamometer force), neurological tests, gait, balance and functional tests. Each value shows its baseline, previous value and change from earlier signed notes; recorded values are never changed.</Term>
            <Term name="Special tests">Search the library or tick Favorites only; ☆ marks a favorite. A red notice shows any precaution before you record the result (positive, negative, not tested, and a number where the test has one). Your interpretation is free text — PhysioTrac does not interpret results.</Term>
            <Term name="Show last visit’s findings">Overlays the last signed visit’s findings as dashed circles and lists them.</Term>
            <Note>Findings are saved as data (region, side, point, type, severity), not as a picture, and are locked with the note when it is signed.</Note>
          </>
        ),
      },
      {
        id: "flowsheet",
        title: "Intervention flowsheet",
        body: (
          <>
            <Term name="Add intervention">Type a name or CPT code; Enter adds the first match from the approved library with its default dose. With no match, Enter adds what you typed. ☆ marks a favorite; favorites show as one-click buttons.</Term>
            <Term name="Groups">“+ group name” adds a saved set of interventions at once. “Save as group…” saves the current entries for reuse (administrators can share a group with the clinic).</Term>
            <Term name="Each entry">Category, CPT, timed or untimed, start/end (minutes fill in from the times), units, sets, reps, resistance, duration, distance, position, equipment, assistance, cueing, modification, pain before/after, status (completed, modified, held, discontinued), patient response and comments. Last visit’s dose is shown underneath.</Term>
            <Term name="Carry forward">Brings chosen entries from the last signed visit — never the whole note. Each carried entry is marked and must be ticked “reviewed” before the note can be signed; carrying forward is recorded in the audit log.</Term>
            <Term name="Totals and warnings">Timed minutes, untimed services and estimated units under your clinic’s 8-minute rule, with warnings for overlapping times, missing responses, minutes that don’t match the times, and units that don’t match the minutes. These are advice only — nothing is billed or submitted from the flowsheet.</Term>
          </>
        ),
      },
      {
        id: "episode",
        title: "Progress notes, re-evaluations, recertifications and discharge",
        body: (
          <>
            <Term name="From the patient’s signed charting">The reporting period (since the last progress note, or since the evaluation), signed visits, attendance from the schedule, the plan of care, and — under “Changes over the episode” — pain, measurements, outcome scores, goals, comparisons with the evaluation and the last progress note, and the home program. Only signed notes are used.</Term>
            <Term name="Fill empty fields from charting">Writes that information into the note’s empty fields (period, visits, changes, functional improvement or outcome, frequency, duration, certification dates, home program) and adds the episode’s goals so you can record their status. Fields you have already written are never replaced. Reason for discharge, prognosis and your assessment are left for you.</Term>
            <Term name="Review before signing">A pre-filled note can’t be signed until you press “I have reviewed the pre-filled content”. Edit anything that isn’t right first.</Term>
            <Term name="What signing does">A re-evaluation or recertification creates a new plan of care; the previous one is kept as superseded. A discharge summary closes the plan of care with the reason for discharge. A progress note doesn’t change the plan — if its frequency or duration differ, the signing checks suggest a recertification.</Term>
          </>
        ),
      },
      {
        id: "plan-of-care",
        title: "Evaluations and the plan of care",
        body: <Term name="Plan of care">Signing an evaluation, re-evaluation or recertification creates the patient’s plan of care from its certification dates, frequency, duration, diagnosis, prognosis and planned interventions. A previous plan is kept as superseded. Goals added during the evaluation join the new plan.</Term>,
      },
      {
        id: "subjective",
        title: "Subjective",
        body: (
          <>
            <Term name="Pain now / best / worst">Tap 0–10. Use Clear to remove a rating.</Term>
            <Term name="Home exercise program">Whether the patient is doing their exercises.</Term>
            <Term name="Patient report">Free text: symptoms since the last visit, function, goals.</Term>
          </>
        ),
      },
      {
        id: "exam",
        title: "Examination",
        body: (
          <>
            <Term name="Range of motion">Choose a region and side, then “+ Add … motions” to add the usual motions for it; enter AROM and PROM in degrees. The normal range and last visit’s value are shown beside each.</Term>
            <Term name="Re-measure last visit’s ROM & strength">Adds the same motions and muscles as last visit with empty values, ready to measure again.</Term>
            <Term name="Strength (MMT)">Add a muscle or movement, choose the side and the 0–5 grade.</Term>
            <Term name="Special tests and vitals">Record each test as Positive or Negative; blood pressure, heart rate, SpO₂.</Term>
            <Term name="Objective findings">Free text. Required before signing.</Term>
          </>
        ),
      },
      {
        id: "outcomes",
        title: "Outcome measures",
        body: (
          <>
            <Term name="Record a measure">Choose the measure — LEFS, Oswestry (ODI), Neck Disability Index, QuickDASH, Patient-Specific Functional Scale, Berg, Timed Up and Go, Five Times Sit-to-Stand, ABC Scale or Functional Gait Assessment. Administer the official form and enter each item’s score (“Item by item”); the score and its interpretation update as you go and the responses are saved with it. “Total only” records a total you already have.</Term>
            <Term name="Comparison">For each measure: baseline (first score), previous and current score, change from baseline, the interpretation, and whether the change reaches the measure’s meaningful-change value. The trend chart plots every score; the dashed line is the baseline. “Score history” lists them all.</Term>
            <Term name="Signed notes">A score recorded on a note becomes part of it: once the note is signed it can’t be changed or removed. Interpretations are commonly cited reference values — clinical judgement applies.</Term>
          </>
        ),
      },
      {
        id: "interventions",
        title: "Interventions",
        body: (
          <>
            <Term name="+ Add intervention">Category, what was done, body region, minutes, whether it is a timed service, and the patient’s response. Use the ☰ menu on a row to edit or remove it.</Term>
            <Term name="Minutes and units">The blue line totals the timed minutes and shows the billable units under your organization’s 8-minute rule.</Term>
          </>
        ),
      },
      {
        id: "progress",
        title: "Goals & progress",
        body: (
          <>
            <Term name="This visit">For each goal enter today’s value, its status (not started, in progress, met, partially met, discontinued) and a comment. The progress bar shows how far the patient has moved from baseline to target. Progress is saved with the note and reaches the goal when the note is signed.</Term>
            <Term name="Insert goal progress">Writes the goals ticked “Include in note” into the progress, functional-improvement or assessment field, ready to edit.</Term>
            <Term name="+ Add goal / Edit goal">Write goals as what the patient will do, how much, how it’s measured and by when; advice appears under the form if the wording isn’t measurable. New goals are drafts until a PT approves them. Editing creates a new version — earlier versions stay in the goal’s history, and signed notes keep the goal as they documented it.</Term>
            <Term name="History">Every version of the goal and every progress entry, with the note it came from.</Term>
          </>
        ),
      },
      {
        id: "sign",
        title: "Signing",
        body: (
          <>
            <Term name="Before signing">Lists anything still required (for example Objective, Plan, or the plan of care on evaluations).</Term>
            <Term name="Sign note">Tick the attestation and re-enter your password. Signing locks the note and completes the visit. If someone else saved the note after you loaded it, signing is refused so you never sign content you haven’t seen — reload and check it first.</Term>
            <Term name="Sign and submit for PT review">For assistants: your signature submits the note to a supervising PT. The system decides when a PT must cosign — always for evaluations, re-evaluations, recertifications, progress notes, discharge summaries and plans of care, and for other notes when your organization requires it.</Term>
            <Term name="Statuses">Draft → Ready to sign (nothing missing) → Signed. An assistant’s note goes Cosign required → In review → Cosigned, or Returned for correction. Signed notes can later be Amended, Locked or Voided.</Term>
            <Term name="Returned for correction">The PT’s reason is shown at the top of the note. Correct it and sign again to resubmit; your earlier signature stays in the note’s history.</Term>
            <Note>Only therapists, assistants and administrators can sign. Every signature records the signer, credentials, role, what the signature means, the date and time (UTC and your clinic’s time zone) and the exact version signed.</Note>
          </>
        ),
      },
      {
        id: "ai-drafting",
        title: "Drafting with AI",
        body: (
          <>
            <Term name="Draft assessment / plan with AI">Under the Assessment and Plan fields of a note you are writing. AI drafts text from this visit’s structured charting — pain, measurements, special tests, interventions, goal progress and outcome scores. It is not given the patient’s name, date of birth, MRN or your narrative.</Term>
            <Term name="The suggestion">Appears in a yellow box and is not part of the note. Choose Insert into note (or Add below my text / Replace my text), or Discard. Inserted text is edited and saved like anything you type.</Term>
            <Term name="[Clinician: …]">Marks what only you can judge, such as the patient’s response and why skilled therapy is needed. Replace each one with your own words.</Term>
            <Term name="Your responsibility">AI never signs, cosigns or finalizes a note and does not diagnose. You review every statement and sign as usual. A signed note that contains inserted AI text is marked AI-assisted in the audit log.</Term>
          </>
        ),
      },
      {
        id: "after-signing",
        title: "After signing",
        body: (
          <>
            <Term name="Start review">For a PT: marks an assistant’s submitted note as in review, so others can see you have it.</Term>
            <Term name="Cosign note">Review the note, re-enter your password and cosign. The visit then completes.</Term>
            <Term name="Return for correction…">Sends the note back to the assistant with what needs correcting (required). It can be edited again and resubmitted.</Term>
            <Term name="Void this note…">For a note that shouldn’t be part of the record, such as one written for the wrong patient. A reason is required; a signed note also needs your password, and only its author PT or an administrator can void it. The note is kept, marked Voided. A plan of care it created is voided and the plan it replaced becomes active again. Voiding can’t be undone.</Term>
            <Term name="+ Add addendum">Adds a dated late entry or clarification under the signed note, with a reason. The signed note itself doesn’t change. The note’s author, the PT who cosigned it, or an administrator or director can add one.</Term>
            <Term name="Amend note">Corrects the signed note: enter the reason, and you get a copy of the note to edit and sign. Once the amendment is signed it replaces the original, which stays viewable marked “Amended”.</Term>
            <Term name="Lock note">Administrators and directors: closes the note to further addenda and amendments (for example when billing has closed).</Term>
            <Term name="Print / PDF">Opens the note as a printable document with the signature block, and the clinic, patient, MRN and page numbers on every page. Use Print, or Save as PDF for a file. Times are shown in the clinic’s time zone. Every print and export is recorded in the audit log.</Term>
            <Term name="Version history">Every saved version of the note, including each autosave and the signed version, with who saved it and when.</Term>
          </>
        ),
      },
    ],
  },

  templates: {
    title: "Documentation Templates",
    intro: (
      <>
        <p>The forms clinical notes are written with: which sections and fields a note has, which are required, and their choices and rules.</p>
        <Note>Editing a template’s fields saves a new version. Notes already written keep the version they used, so changing a template never changes past documentation.</Note>
      </>
    ),
    sections: [
      {
        id: "list",
        title: "Template list",
        body: (
          <>
            <Term name="System / Clinic">System templates come with PhysioTrac and can’t be changed — use Copy to make your clinic’s own version. Clinic templates are yours to edit.</Term>
            <Term name="★ Favorites">Your favorites are listed first and are suggested first when you start a note.</Term>
            <Term name="Activate / Deactivate">An inactive template is no longer offered for new notes; notes written with it are unaffected.</Term>
          </>
        ),
      },
      {
        id: "editor",
        title: "Editing a template",
        body: (
          <>
            <Term name="Sections">Group fields under a title. A section can include a clinical component (measurements, interventions, goals, body chart…), whose data is recorded in its own structured form.</Term>
            <Term name="Fields">Choose the type (text, number, date, time, checkbox, radio, select, multiselect, measurement, pain scale, table, signature), whether it’s required to sign, help text, choices and limits.</Term>
            <Term name="Store in the note’s">Puts a text field in the note’s Subjective, Objective, Assessment, Plan or Treatment summary.</Term>
            <Term name="Show only when">Makes a field conditional: it appears only when another field has a given answer. A hidden field is never required.</Term>
            <Term name="Suggest for these appointment types">The template is offered first for those visits.</Term>
            <Term name="Preview">Try the form as it will appear on a note.</Term>
            <Term name="Version history">Every version, who published it, what changed, and how many notes use it.</Term>
          </>
        ),
      },
    ],
  },

  "special-tests": {
    title: "Special Tests Library",
    intro: <p>The special tests clinicians pick from when charting. Built-in tests come with PhysioTrac; add your clinic’s own as needed.</p>,
    sections: [
      {
        id: "manage",
        title: "Managing tests",
        body: (
          <>
            <Term name="+ Add test">Name, specialty, body region, the kind of result (positive/negative, a number with its unit, or both), a description, an interpretation guide and any contraindication warning.</Term>
            <Term name="Deactivate">Stops offering the test for new notes. Results already recorded keep their own copy of the name.</Term>
            <Term name="⚠">The test has a precaution that clinicians see before recording it.</Term>
            <Note>Interpretation guides are reference text for clinicians; PhysioTrac never interprets a result or makes a diagnosis.</Note>
          </>
        ),
      },
    ],
  },

  "patient-documentation": {
    title: "Patient Documentation",
    intro: <p>Everything documented for one patient: visit notes with their charting, pain and range-of-motion trends, outcome measures, goals, diagnoses and precautions.</p>,
    sections: [
      {
        id: "glance",
        title: "At a glance",
        body: <Term name="Progress note">“Due now” when a progress note is due (by number of visits or days since the last one).</Term>,
      },
      {
        id: "trends",
        title: "Trends",
        body: <Term name="Pain & range-of-motion trend">Pain and each measured motion across the last six signed visits, oldest to newest.</Term>,
      },
      {
        id: "outcomes-goals",
        title: "Outcome measures & goals",
        body: (
          <>
            <Term name="Outcome measures">Each measure’s baseline, previous and current score, change from baseline, interpretation, trend chart and full history.</Term>
            <Term name="Goals">Every goal with its status and progress; “History” shows each version and progress entry.</Term>
          </>
        ),
      },
      {
        id: "notes",
        title: "Visit notes",
        body: (
          <>
            <Term name="▸ Date — note type">Click to expand the full note: pain, measurements, interventions with patient response, and the narrative.</Term>
            <Term name="Open note / Continue charting">Opens the note in Clinical Charting.</Term>
            <Term name="+ New note">Starts a note that isn’t tied to an appointment — a phone call, consultation, missed visit or addendum.</Term>
          </>
        ),
      },
      {
        id: "reports",
        title: "Print reports",
        body: (
          <>
            <Term name="Reports">Plan of care (with its goals), body chart history (signed notes only), measurement comparison across visits, goal progress and outcome measure history. Each opens as a printable page with the clinic, patient, MRN and page numbers on every page.</Term>
            <Term name="Print / Save as PDF">Every print and export is recorded in the audit log.</Term>
          </>
        ),
      },
    ],
  },

  billing: {
    title: "Billing",
    intro: <p>Billing is not available in this version yet.</p>,
    sections: [],
  },

  admin: {
    title: "Administration",
    intro: (
      <>
        <p>Settings and records for your organization.</p>
        <Note>You only see the buttons your access level allows.</Note>
      </>
    ),
    sections: [
      {
        id: "buttons",
        title: "Buttons",
        body: (
          <>
            <Term name="Change Password">Change your own password.</Term>
            <Term name="Messages">Secure messages with patients.</Term>
            <Term name="Users">Add staff, and edit their details, access level, status and password.</Term>
            <Term name="Locations">Add and edit clinic locations.</Term>
            <Term name="Patient List">Add, edit, delete and restore patients.</Term>
            <Term name="Logs">Everything that happened: changes, sign-ins and screens opened, by every user.</Term>
          </>
        ),
      },
    ],
  },

  logs: {
    title: "Logs",
    intro: (
      <>
        <p>The activity log for your organization: every change to a record, every sign-in and sign-out (including failed attempts), and every screen each user opened — newest first.</p>
        <Note>Entries can’t be edited or deleted. Changed fields are listed by name; their values are not stored in the log.</Note>
      </>
    ),
    sections: [
      {
        id: "filters",
        title: "Filters",
        body: (
          <>
            <Term name="Today / Yesterday / Last 7 Days / Last 30 Days">Quick date ranges. Use From / To for others (up to 93 days at a time).</Term>
            <Term name="User">Only one person’s activity.</Term>
            <Term name="Category">Sign-in / sign-out, screens opened, patients, schedule, clinical notes, messages, billing, administration.</Term>
            <Term name="Search">Matches the activity, user, patient name, MRN or IP address.</Term>
          </>
        ),
      },
      {
        id: "entries",
        title: "Reading an entry",
        body: (
          <>
            <Term name="Activity">What was done, for example “Checked a patient in” or “Updated patient: phone, address”.</Term>
            <Term name="System">Shown as the user for changes the system made by itself.</Term>
            <Term name="Show details">Click the activity (or use the ☰ menu) for the full entry, including the record’s ID and the technical details.</Term>
          </>
        ),
      },
      {
        id: "export",
        title: "Export",
        body: (
          <>
            <Term name="Export CSV">Downloads the entries matching your filters (up to 5,000) as a spreadsheet file.</Term>
            <Note>The export can contain patient names — store and share it as you would any patient information.</Note>
          </>
        ),
      },
      listsSection,
    ],
  },

  users: {
    title: "Users",
    intro: <p>Staff accounts for your organization.</p>,
    sections: [
      {
        id: "filters",
        title: "Filters",
        body: (
          <>
            <Term name="All Users / Active Users / Invited / Suspended Users / Deleted Users">Filter by account status.</Term>
            <Term name="Search User">Matches name, user ID or e-mail.</Term>
          </>
        ),
      },
      {
        id: "add",
        title: "Adding a user",
        body: (
          <>
            <Term name="+ New user">Enter name, e-mail and role. The account is created without a password.</Term>
            <Term name="Activation link">Copy the one-time link shown and send it to the new user; they set their own password with it. Until then they are listed as Invited.</Term>
            <Note>E-mail sending is not connected yet, so the link has to be sent by you.</Note>
          </>
        ),
      },
      {
        id: "edit",
        title: "Edit User",
        body: (
          <>
            <Term name="Open">Click the user’s name, or choose Edit User from the ☰ menu.</Term>
            <Term name="User ID">What they type to sign in. Must be unique.</Term>
            <Term name="New Password">Leave blank to keep their password. Setting one signs them out everywhere; give them the new password yourself.</Term>
            <Term name="Status">Active can sign in. Suspended and Deleted cannot, and are signed out immediately.</Term>
            <Term name="Access Level">What they are allowed to do.</Term>
            <Note>You can’t change your own status or access level.</Note>
          </>
        ),
      },
      listsSection,
    ],
  },

  "documentation-settings": {
    title: "Documentation Settings",
    intro: <p>Clinic-wide documentation rules. Administrators and directors can change them; others can view them.</p>,
    sections: [
      {
        id: "progress",
        title: "Progress notes",
        body: (
          <>
            <Term name="Treatment visits before a progress note is due">After this many signed treatment visits since the evaluation or last progress note, the progress note shows as due on the Documentation Dashboard and in the patient’s documentation.</Term>
            <Term name="Days before a progress note is due">Counted from the evaluation or last progress note. A progress note is due at whichever comes first. An empty visits box uses the standard 10th visit; an empty days box turns the day trigger off.</Term>
          </>
        ),
      },
      {
        id: "signing",
        title: "Signing",
        body: (
          <>
            <Term name="Assistants’ notes need a PT cosignature">When on, an assistant’s signed note goes to a PT for review and cosign before it is final. Applies to notes started after the change. Evaluations, re-evaluations, recertifications, progress notes and discharges written by an assistant always need one.</Term>
            <Term name="Create pending charges when a note is signed">When a note becomes final, Draft charges are created from its billable interventions for billing to review. Nothing is submitted automatically. An assistant’s note is charged when cosigned; an amendment doesn’t charge the visit again. If billing isn’t set up (for example a missing CPT mapping), the note is still signed and the Logs page records what was skipped.</Term>
          </>
        ),
      },
    ],
  },
  locations: {
    title: "Locations",
    intro: <p>Your organization’s clinic locations. They appear in the schedule, patient records and provider hours.</p>,
    sections: [
      {
        id: "add-edit",
        title: "Adding and editing",
        body: (
          <>
            <Term name="+ Add location">Opens the Add Location window. Location Name is required (marked in red until filled); time zone, phone, address, NPI and Tax ID can be filled in now or later.</Term>
            <Term name="Edit">Click the location’s name, or choose Edit from the ☰ menu, to open the Edit Location window. Save and Close becomes available once you change something.</Term>
            <Note>The time zone is what the schedule uses for that clinic.</Note>
          </>
        ),
      },
      {
        id: "deactivate",
        title: "Deactivate and reactivate",
        body: (
          <>
            <Term name="Deactivate">From the ☰ menu. The location is no longer offered for booking; nothing is deleted.</Term>
            <Term name="All (incl. inactive)">Shows deactivated locations, so you can reactivate one.</Term>
          </>
        ),
      },
      listsSection,
    ],
  },

  messages: {
    title: "Messages",
    intro: <p>Secure messages with patients, one conversation per patient.</p>,
    sections: [
      {
        id: "inbox",
        title: "Conversations",
        body: (
          <>
            <Term name="Left side">Each patient with messages, newest first. A number shows unread messages.</Term>
            <Term name="Open a conversation">Click a patient. Their messages are marked as read.</Term>
          </>
        ),
      },
      {
        id: "send",
        title: "Sending",
        body: (
          <>
            <Term name="Reply">Type in the box and press Send (or Ctrl+Enter).</Term>
            <Term name="+ New message">Search for a patient to start a conversation.</Term>
          </>
        ),
      },
    ],
  },

  "change-password": {
    title: "Change Password",
    intro: <p>Change the password you sign in with.</p>,
    sections: [
      {
        id: "rules",
        title: "Password rules",
        body: (
          <>
            <Term name="New password">At least 10 characters, with an uppercase letter, a lowercase letter, a number and a symbol. The form shows what is still missing as you type.</Term>
            <Term name="Eye button">Shows or hides what you typed.</Term>
            <Note>You stay signed in on this device after changing it.</Note>
          </>
        ),
      },
    ],
  },
};

/** Which help applies to a page address. */
export function helpKeyFor(pathname: string): string {
  if (pathname === "/") return "home";
  if (pathname.startsWith("/schedule/hours")) return "provider-hours";
  if (pathname.startsWith("/schedule")) return "schedule";
  if (pathname.startsWith("/chart/")) return "charting";
  if (/^\/patients\/[^/]+\/documentation/.test(pathname)) return "patient-documentation";
  if (pathname.startsWith("/patients")) return "patients";
  if (pathname.startsWith("/providers")) return "providers";
  if (pathname.startsWith("/billing")) return "billing";
  if (pathname.startsWith("/workflow")) return "workflow";
  if (pathname.startsWith("/documentation")) return "documentation-dashboard";
  if (pathname.startsWith("/admin/patients")) return "admin-patients";
  if (pathname.startsWith("/admin/users")) return "users";
  if (pathname.startsWith("/admin/logs")) return "logs";
  if (pathname.startsWith("/admin/templates")) return "templates";
  if (pathname.startsWith("/admin/special-tests")) return "special-tests";
  if (pathname.startsWith("/admin/documentation-settings")) return "documentation-settings";
  if (pathname.startsWith("/admin/locations")) return "locations";
  if (pathname.startsWith("/admin/messages")) return "messages";
  if (pathname.startsWith("/admin/change-password")) return "change-password";
  if (pathname.startsWith("/admin")) return "admin";
  return "home";
}

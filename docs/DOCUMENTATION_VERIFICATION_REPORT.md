# PhysioTrac — Clinical Documentation Verification Report

Final verification (step 14) of the 14-step Clinical Documentation roadmap.
It records what was built, how each part was checked, what the checks found,
and what is still open.

**Date:** 8 October 2026 · **Branch:** `feature/scheduling-module` ·
**Code verified:** commits `89acb22` → `6725701`, plus the fixes listed in
section 5.

> **Not a compliance statement.** This report does not claim, and PhysioTrac
> does not have, HIPAA compliance. HIPAA compliance depends on how an
> organization operates the system — risk analysis, policies, workforce
> training, business associate agreements, hosting and backups — not on the
> software alone. See [HIPAA_CHECKLIST.md](HIPAA_CHECKLIST.md). All data used
> in this verification is fictional.

---

## 1. Summary

| | Result |
|---|---|
| Backend automated tests (xUnit) | **755 passed**, 0 failed |
| Frontend automated tests (Vitest) | **164 passed**, 0 failed |
| TypeScript type check | Clean |
| End-to-end run against SQL Server (real API + browser) | **56 / 56 checks passed** |
| Phones, tablet and desktop (no horizontal scrolling, no page errors) | Passed on iPhone 13, Pixel 7, iPad and 1440 px desktop |
| Bugs found during verification | 2, both fixed and covered by tests (section 5) |

---

## 2. Environment

- **Database:** SQL Server Express (`.\SQLEXPRESS`), migrated to the latest
  migration (`AuditEventObjectIndex`).
- **API:** ASP.NET Core 8 on `http://localhost:5080`, Development settings.
- **Web app:** React (Vite) on `http://localhost:5173`.
- **Browser automation:** Playwright 1.64 (Chromium), with iPhone 13, Pixel 7
  and iPad device profiles.
- **Access control:** `AccessControl:Enabled = false` in Development. Role
  limits are relaxed for everyday use here. Clinical signing, cosigning and
  locking still enforce real roles. Role rules are verified by the automated
  tests (section 4).
- **AI provider:** `DocumentationAi:Provider = Mock`. Nothing is sent outside
  the server.

---

## 3. The demo episode

The run builds one complete episode of care for a fictional patient
(**Riley Demoson-…**, a new suffix each run) at Source Motion Physical
Therapy. It uses the real API and web app, as these demo users:

| Who | Demo login | Role |
|---|---|---|
| Jamie Chen, PT, DPT | `therapist` | Physical therapist (evaluates, cosigns) |
| Avery Kim, PTA | `assistant` | PT assistant (treats, submits for cosign) |
| Alex Rivera | `admin` | Books visits, reads the audit log |
| Casey Nguyen | `biller` | Print-permission check |
| Priya Sharma (Total Motion PT) | `tm.therapist` | Second clinic, tenant-isolation check |

The episode:

1. **Initial evaluation** (24 days ago): pain, body chart, knee range of
   motion and strength, Lachman test, LEFS outcome score, two goals, plan of
   care 2×/week for 6 weeks. Signed with password re-entry. Signing creates
   the plan of care.
2. **Three PTA daily visits**: pain before and after treatment, range of
   motion, flowsheet (97110 and 97140), and goal progress. On the first visit
   the PTA uses the AI draft for the assessment and replaces the
   `[Clinician: …]` prompt with their own words.
3. **PT review**: the PTA signs and each note goes to review. The PT starts
   reviewing one and returns it for correction with a reason. The PTA fixes
   it and resubmits. The PT cosigns all three with password re-entry.
4. A note opened in error: signing with a wrong password is refused, then
   the note is **voided** with a reason.
5. A **no-show** visit: a treatment note is refused and a missed-visit note
   is allowed.
6. An **addendum** from the author on a signed note. An **amendment** of the
   evaluation, whose signing marks the original as Amended.
7. **Progress note** (visit 5): prefilled from the signed episode, prefill
   reviewed, goal progress and a second LEFS score, then signed. Signing
   applies the goal values.
8. **Discharge**: prefilled, goals met, signed. Signing closes the plan of
   care.
9. **Printing**: the note and all five patient reports printed, plus a PDF
   of each kind generated and read back.

---

## 4. Results by roadmap step

Each row lists how the step was checked: **Auto** = automated tests,
**Live** = the end-to-end run on SQL Server.

| # | Step | What was checked | Result |
|---|---|---|---|
| 1 | Database foundation | Migrations apply cleanly. Enum column widths checked against the relational model (`EnumColumnLengthTests`). One note system (`ClinicalNote`), no duplicate tables. | Pass (Auto, Live) |
| 2 | Template system | 21 system templates present and versioned. A template edit creates a new version, and notes keep the version they were written with. | Pass (Auto, Live) |
| 3 | Initial Evaluation | An evaluation visit opens the evaluation template. Required fields block signing. Signing creates the plan of care and links the goals. | Pass (Auto, Live) |
| 4 | Daily SOAP / encounter workspace | A note opens from its appointment. Autosave keeps a version per save. A save conflict returns 409. Signed notes can't be edited. Addenda and amendments work. | Pass (Auto, Live) |
| 5 | Pain + body chart | Pain scales are validated. Body-chart findings are saved and printed. History comes from signed notes only. | Pass (Auto, Live) |
| 6 | Measurements + special tests | Range-of-motion and strength measurements and special tests are saved. The measurement comparison report shows 95° → 110° across visits. | Pass (Auto, Live) |
| 7 | Intervention flowsheet | Timed and untimed entries. Carried-forward entries need review. 8-minute-rule figures are advice only. | Pass (Auto, Live) |
| 8 | Goals + outcome measures | Goals approved as Not started. Goal progress is applied at signing. LEFS scored. Goal statuses run through to Met. | Pass (Auto, Live) |
| 9 | Progress / re-evaluation / discharge | Prefill fills only empty fields and must be reviewed. Discharge closes the plan of care. The visit count runs across an evaluation amendment (fixed, section 5). | Pass (Auto, Live) |
| 10 | Lifecycle, signatures, PTA review | Sign, submit for cosign, start review, return for correction, resubmit, cosign, void. Wrong password refused. | Pass (Auto, Live) |
| 11 | Schedule integration + dashboard | Calendar appointments carry their note and documentation status. Missed-visit rule. Dashboard loads with filters and deadlines. | Pass (Auto, Live) |
| 12 | Audit + print/PDF | All 13 lifecycle event types are recorded, each with readable Logs wording. No note text in any audit row. Prints are recorded before the dialog opens. Every PDF page has clinic, patient, MRN, title and "Page n of N". | Pass (Auto, Live) |
| 13 | AI-assisted documentation | The mock provider drafts from structured data only (no name, DOB, MRN or narrative). The note is unchanged until Insert. Signing records `aiAssisted`. Drafting is refused on signed notes, for non-editors and for other clinics. | Pass (Auto, Live) |
| 14 | Final verification | This report. | — |

**Security checks in the live run**

- A therapist at the second clinic gets **404** when opening or AI-drafting
  this clinic's notes.
- A signed note can't be edited through the API.
- The cosigning PT can't add an addendum to the PTA's note. Only the author,
  or an admin or director, can.

---

## 5. Problems found during verification

| # | Problem | Impact | Fix | Covered by |
|---|---|---|---|---|
| 1 | Amending a signed evaluation creates a new plan of care. The visit number then counted only notes under the new plan, so the next progress note showed **"Visit #1"** instead of visit 5. | Wrong visit number in the encounter header. It could mislead a clinician tracking visits in the episode. | Visits under a plan that was replaced *because its evaluation was amended* now count as the same episode. A re-evaluation or recertification still starts a new count. | `InitialEvaluationTests.AmendingTheEvaluation_KeepsCountingTheSameEpisode`. Live check "Progress note is visit 5". |
| 2 | Under heavy local load, one request that records a note view timed out (34 s) while SQL Server Express was unresponsive. The audit table had no index for per-note lookups. | Possible slow note opening as the audit log grows. | Added index `IX_AuditEvents_ObjectId_Action_CreatedAt` (migration `AuditEventObjectIndex`). It serves view and edit de-duplication and the AI-assisted check at signing. | Live run repeated cleanly after the migration. |

Earlier in this work, the end-to-end runs also found that printed times
ignored the clinic's time zone, and that the step-12 page headers and page
numbers were missing. Both were fixed before step 14 (commits `7228bc0` and
`6725701`).

---

## 6. Open items and decisions to confirm

1. **Access control is off in Development.** Turn it on (`AccessControl:
   Enabled = true`) and repeat the role checks before any real use. With it
   off, a biller can print a clinical note. The print is still recorded, and
   the role rule itself is tested.
2. **Addenda by the cosigning PT.** Today only the note's author, or an admin
   or director, can add an addendum. Some clinics expect the supervising PT
   to be able to add one to a PTA note. This needs a decision.
3. **Pending charges at signing.** Charges are created on request (`POST
   /api/v1/charges/generate-from-note/{noteId}`), not automatically when a note is signed, as decided
   earlier. No claims are ever submitted automatically.
4. **Progress-note visit threshold.** The organization setting exists and is
   used, but there is no admin screen for it yet.
5. **AI provider.** Only the mock provider exists. A real provider must not
   receive patient data until there is a business associate agreement and a
   privacy review of what is sent. The context is already limited to
   structured clinical data.
6. **Page headers and numbers** print in Chrome and Edge. Other browsers
   leave them off but keep the first-page letterhead.
7. **Automated tests use an in-memory database**, which ignores unique
   indexes and SQL translation. The live SQL Server run covers that gap.
   Repeat it after schema changes.
8. **Test data in the development database.** Verification runs left
   fictional patients named `Zz…test` and `Riley Demoson-…`. They inflate the
   dashboard's "Overdue" and "Notes not started" counts. Clear them before a
   demo.

---

## 7. How to repeat this verification

1. `dotnet test` from the repository root (backend), and `npm test` plus
   `npx tsc -b` in `frontend/`.
2. Start the API in Development (it migrates and seeds on startup) and the
   web app (`npm run dev` in `frontend/`).
3. Run the end-to-end script with the demo password in `DEMO_PW`. The script
   is kept outside the repository with the other session scripts; ask for it
   to be added under `tests/` if you want it versioned.

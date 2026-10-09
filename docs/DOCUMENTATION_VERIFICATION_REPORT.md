# PhysioTrac — Clinical Documentation Verification Report

Final verification (step 14) of the 14-step Clinical Documentation roadmap,
updated after the follow-up work on the open items. It records what was
built, how each part was checked, what the checks found, and what is still
open.

**Date:** 8 October 2026 · **Branch:** `feature/scheduling-module` ·
**Code verified:** commits `89acb22` → `ce67cf1`, plus the follow-up changes
in sections 5 and 6.

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
| Backend automated tests (xUnit) | **762 passed**, 0 failed |
| Frontend automated tests (Vitest) | **167 passed**, 0 failed |
| TypeScript type check | Clean |
| End-to-end run, role checks **on** (real API, browser, SQL Server) | **70 / 70 checks passed** |
| End-to-end run, role checks **off** (normal development setting) | **65 / 65 checks passed** |
| Phones, tablet and desktop (no horizontal scrolling, no page errors) | Passed on iPhone 13, Pixel 7, iPad and 1440 px desktop |
| Bugs found during verification | 3, all fixed and covered by tests (section 5) |

---

## 2. Environment

- **Database:** SQL Server Express (`.\SQLEXPRESS`), migrated to the latest
  migration (`OrganizationPendingChargesSetting`).
- **API:** ASP.NET Core 8 on `http://localhost:5080`, Development settings.
- **Web app:** React (Vite) on `http://localhost:5173`.
- **Browser automation:** Playwright 1.64 (Chromium), with iPhone 13, Pixel 7
  and iPad device profiles.
- **Access control:** the end-to-end run was done twice. Once with
  `AccessControl:Enabled = true` (real role limits, as in production), and
  once with the Development default (`false`). Viewing, signing, cosigning and
  locking notes are limited to clinical roles in both modes.
- **Rate limits:** raised for the test process only
  (`Security__GlobalRateLimitPermitsPerWindow`,
  `Security__AuthRateLimitPermitsPerWindow`), because every test browser
  shares one local address. The limits themselves were seen working (429
  responses) before they were raised.
- **AI provider:** `DocumentationAi:Provider = Mock`. Nothing is sent outside
  the server.

---

## 3. The demo episode

Each run builds one complete episode of care for a fictional patient
(**Riley Demoson-…**, a new suffix each run) at Source Motion Physical
Therapy, through the real API and web app. The episode from the last run,
**Riley Demoson-237F**, is kept in the development database as the demo
patient. Patients left by earlier runs were deleted (section 6).

| Who | Demo login | Role |
|---|---|---|
| Jamie Chen, PT, DPT | `therapist` | Physical therapist (evaluates, cosigns) |
| Avery Kim, PTA | `assistant` | PT assistant (treats, submits for cosign) |
| Alex Rivera | `admin` | Books visits, configures billing codes, reads the audit log |
| Morgan Patel | `scheduler` | Non-clinical role checks |
| Casey Nguyen | `biller` | Role checks (see the note in section 6) |
| Priya Sharma (Total Motion PT) | `tm.therapist` | Second clinic, tenant-isolation check |

The episode:

1. **Initial evaluation**: pain, body chart, knee range of motion and
   strength, Lachman test, LEFS outcome score, two goals, plan of care
   2×/week for 6 weeks. Signed with password re-entry. Signing creates the
   plan of care.
2. **Three PTA daily visits**: pain before and after treatment, range of
   motion, flowsheet (23 min therapeutic exercise, 15 min manual therapy) and
   goal progress. On the first visit the PTA uses the AI draft for the
   assessment and replaces the `[Clinician: …]` prompt with their own words.
3. **PT review**: the PTA signs and each note goes to review. The PT starts
   reviewing one and returns it for correction with a reason. The PTA fixes
   it and resubmits. The PT cosigns all three with password re-entry. Each
   cosign creates **Draft charges** (97110 × 2 units, 97140 × 1 unit) for
   billing to review.
4. A note opened in error: signing with a wrong password is refused, then
   the note is **voided** with a reason.
5. A **no-show** visit: a treatment note is refused and a missed-visit note
   is allowed.
6. **Addenda** from the author and from the cosigning PT. An **amendment** of
   the evaluation, whose signing marks the original as Amended and creates no
   new charges.
7. **Progress note** (visit 5): prefilled from the signed episode, prefill
   reviewed, goal progress and a second LEFS score, then signed. Signing
   applies the goal values.
8. **Discharge**: prefilled, goals met, signed. Signing closes the plan of
   care.
9. **Printing**: the note and all five patient reports printed, plus a PDF
   of each kind generated and read back.
10. **Documentation settings**: changed and restored by an administrator;
    out-of-range values refused.

---

## 4. Results by roadmap step

Each row lists how the step was checked: **Auto** = automated tests,
**Live** = the end-to-end runs on SQL Server.

| # | Step | What was checked | Result |
|---|---|---|---|
| 1 | Database foundation | Migrations apply cleanly. Enum column widths checked against the relational model (`EnumColumnLengthTests`). One note system (`ClinicalNote`), no duplicate tables. | Pass (Auto, Live) |
| 2 | Template system | 21 system templates present and versioned. A template edit creates a new version, and notes keep the version they were written with. | Pass (Auto, Live) |
| 3 | Initial Evaluation | An evaluation visit opens the evaluation template. Required fields block signing. Signing creates the plan of care and links the goals. | Pass (Auto, Live) |
| 4 | Daily SOAP / encounter workspace | A note opens from its appointment. Autosave keeps a version per save. A save conflict returns 409. Signed notes can't be edited. Addenda and amendments work. | Pass (Auto, Live) |
| 5 | Pain + body chart | Pain scales are validated. Body-chart findings are saved and printed. History comes from signed notes only. | Pass (Auto, Live) |
| 6 | Measurements + special tests | Range-of-motion and strength measurements and special tests are saved. The measurement comparison report shows 95° → 110° across visits. | Pass (Auto, Live) |
| 7 | Intervention flowsheet | Timed and untimed entries. Carried-forward entries need review. 8-minute-rule figures are advice only. Units on the pending charges follow the organization's 8-minute rule. | Pass (Auto, Live) |
| 8 | Goals + outcome measures | Goals approved as Not started. Goal progress is applied at signing. LEFS scored. Goal statuses run through to Met. | Pass (Auto, Live) |
| 9 | Progress / re-evaluation / discharge | Prefill fills only empty fields and must be reviewed. Discharge closes the plan of care. The visit count runs across an evaluation amendment, and "progress note due" uses the same 10-visit default everywhere (both fixed, section 5). | Pass (Auto, Live) |
| 10 | Lifecycle, signatures, PTA review | Sign, submit for cosign, start review, return for correction, resubmit, cosign, void. Wrong password refused. An assistant can't cosign. | Pass (Auto, Live) |
| 11 | Schedule integration + dashboard | Calendar appointments carry their note and documentation status. Missed-visit rule. Dashboard loads with filters and deadlines. Deleted patients are left out of both (confirmed in the code). | Pass (Auto, Live) |
| 12 | Audit + print/PDF | All 13 lifecycle event types are recorded, each with readable Logs wording. Pending-charge events are audited too (automated tests). No note text in any audit row. Prints are recorded before the dialog opens. Every PDF page has clinic, patient, MRN, title and "Page n of N". | Pass (Auto, Live) |
| 13 | AI-assisted documentation | The mock provider drafts from structured data only (no name, DOB, MRN or narrative). The note is unchanged until Insert. Signing records `aiAssisted`. Drafting is refused on signed notes, for non-clinical roles and for other clinics. | Pass (Auto, Live) |
| 14 | Final verification | This report. | — |

**Security and role checks in the live runs**

- A therapist at the second clinic gets **404** when opening or AI-drafting
  this clinic's notes.
- A signed note can't be edited through the API.
- With role checks on, a scheduler can't open a note, use AI drafting, print
  a note or print a patient report. A billing user can't edit a note. A
  therapist can't change documentation settings (all **403**).
- Viewing and printing notes is limited to clinical staff even with role
  checks off.

---

## 5. Problems found during verification

| # | Problem | Impact | Fix | Covered by |
|---|---|---|---|---|
| 1 | Amending a signed evaluation creates a new plan of care. The visit number then counted only notes under the new plan, so the next progress note showed **"Visit #1"** instead of visit 5. | Wrong visit number in the encounter header. It could mislead a clinician tracking visits in the episode. | Visits under a plan that was replaced *because its evaluation was amended* now count as the same episode. A re-evaluation or recertification still starts a new count. | `InitialEvaluationTests.AmendingTheEvaluation_KeepsCountingTheSameEpisode`. Live check "Progress note is visit 5". |
| 2 | With no visit threshold set, the Documentation Dashboard flagged a progress note at the 10th visit, but the patient's own "Progress note" status never did. | The two screens disagreed, and the patient's documentation page could show "Not due" for an overdue progress note. | Both now use the clinic's setting, or the standard 10th visit when none is set. The settings page says so. | `PtNoteTypesPhase5BTests.GetProgressNoteStatus_UsesTheStandardTenthVisit_WhenNoVisitCountIsConfigured`. |
| 3 | The audit table had no index for per-note lookups. Note-view and edit de-duplication and the AI-assisted check at signing scan it. | Note opening and signing would slow down as the audit log grows. | Added index `IX_AuditEvents_ObjectId_Action_CreatedAt` (migration `AuditEventObjectIndex`). | Live runs. |

**Local database stalls (environment, not app code).** During the runs, a few
requests waited 35–65 seconds for SQL Server Express and timed out. Each time
it was a different query. They clustered after quiet periods, and while they
lasted even a separate `sqlcmd` login timed out. Nothing was found blocking
them. Two local causes were found:

- `AUTO_CLOSE` was on for the PhysioTrac database. The database had started
  up 34 times that day. It is now off
  (`ALTER DATABASE PhysioTrac SET AUTO_CLOSE OFF`).
- The machine had about 1 GB of 8 GB free. Windows had trimmed SQL Express to
  71 MB of memory, so its data is read back from disk after a pause.

The final runs used one automatic retry per request and recorded each one.
There was one retry in the role-checks-off run (an audit-log listing) and none
with role checks on.

Earlier in this work, the end-to-end runs also found that printed times
ignored the clinic's time zone, and that the step-12 page headers and page
numbers were missing. Both were fixed before step 14 (commits `7228bc0` and
`6725701`).

---

## 6. Follow-up items

**Done**

1. **Role checks verified on.** The full run passes with
   `AccessControl:Enabled = true` (70 / 70), including the role limits listed
   in section 4. Development still defaults to off; turn it on for any real
   use.
2. **Addenda by the cosigning PT.** The PT who cosigned a note can now add an
   addendum to it, as well as its author and admins/directors. Amendments
   stay with the author or an admin/director.
3. **Pending charges at signing.** When a note becomes final (signed, or
   cosigned for an assistant's note), Draft charges are created from its
   billable interventions for billing to review. Nothing is ever submitted.
   An amendment doesn't charge the visit again. Missing billing setup, such as
   a CPT mapping, never blocks signing; the Logs page records what was
   skipped. Clinics can turn this off.
4. **Documentation Settings screen** (Administration → Documentation
   Settings): progress-note visit and day thresholds, PTA cosign, and pending
   charges. Admins and directors can edit; others can view.
5. **Test data cleared.** 22 test patients from verification runs were
   deleted (hidden, restorable with Restore). The demo patient Riley
   Demoson-237F is kept.

**Still open**

1. **AI provider.** Only the mock provider exists. A real provider must not
   receive patient data until there is a business associate agreement and a
   privacy review of what is sent. The context is already limited to
   structured clinical data.
2. **Page headers and numbers** print in Chrome and Edge. Other browsers
   leave them off but keep the first-page letterhead.
3. **Automated tests use an in-memory database**, which ignores unique
   indexes and SQL translation. The live SQL Server run covers that gap.
   Repeat it after schema changes.
4. **The `biller` demo login has the Admin role** in this development
   database. It was changed on 6 October through the Users page. The seed
   creates it as Biller. Change it back if you want to demo billing-only
   access.
5. **Local SQL Express performance.** See section 5. Free up memory on the
   development machine, or test on a full SQL Server instance before judging
   performance.

---

## 7. How to repeat this verification

1. `dotnet test` from the repository root (backend), and `npm test` plus
   `npx tsc -b` in `frontend/`.
2. Start the API in Development (it migrates and seeds on startup) and the
   web app (`npm run dev` in `frontend/`). For the role-checks-on run, start
   the API with `AccessControl__Enabled=true`.
3. Run the end-to-end script with the demo password in `DEMO_PW`. The script
   is kept outside the repository with the other session scripts; ask for it
   to be added under `tests/` if you want it versioned.

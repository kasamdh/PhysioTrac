# PhysioTrac — Testing Guide

How to start PhysioTrac on a local machine, log in with the demo accounts,
and check each area of the product. It covers manual testing through the
web app and the API, the automated test suite, and how to check results in
the database.

---

## 1. What you are testing

PhysioTrac is a physical-therapy practice-management system. It has three parts:

| Part | What it is | URL when running locally |
|---|---|---|
| **API** | The backend. Every feature is available here. | http://localhost:5080 (Swagger UI at http://localhost:5080/swagger) |
| **Web app** | The main staff user interface (Blazor). | http://localhost:5073 |
| **React app** | A newer UI that is still in progress. Only login and the dashboard are built. | http://localhost:5173 |

The web app covers patients, schedule, providers, appointment types, service
prices, payers, clinical notes and claims. Everything else (billing reports,
dashboards, the patient portal, public booking, SaaS admin and so on) is
tested through **Swagger**.

---

## 2. Before you start

You need:

- **.NET 8 SDK** (or newer, with the .NET 8 runtime installed)
- **SQL Server Express** running as `.\SQLEXPRESS` (or LocalDB / Docker; see the README)
- **Node.js** (only for the React app)
- **SQL Server Management Studio (SSMS)**, optional, for looking at the database

The demo password must be set once per machine:

```powershell
dotnet user-secrets set "Seed:DemoPassword" "DemoPass123!" --project src/PhysioTrac.Api
```

---

## 3. Start the software

Open **three terminals** in the project folder (`PhysioTrac`).

**Terminal 1: API**
```powershell
dotnet run --project src/PhysioTrac.Api --urls http://localhost:5080
```
Wait for `Now listening on: http://localhost:5080`. On first start it
creates the database and loads the demo data automatically.

**Terminal 2: Web app**
```powershell
dotnet run --project src/PhysioTrac.Web
```

**Terminal 3: React app (optional)**
```powershell
cd frontend
npm install     # first time only
npm run dev
```

To stop any part, press **Ctrl+C** in its terminal.

If you see *"address already in use"*, an old copy is still running. Stop it with:
```powershell
Get-Process PhysioTrac.Api, PhysioTrac.Web -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## 4. Test accounts

Every account uses the password **`DemoPass123!`**. You can log in with either the username or the email.

There are two separate clinics (called *organizations* or *tenants*). They must never see each other's data.

| Role | Source Motion PT | Total Motion PT | What this role is for |
|---|---|---|---|
| Admin | `admin` | `tm.admin` | Full control of the clinic, users and settings |
| Director | `director` | `tm.director` | Clinic director: reports, oversight |
| Therapist | `therapist` | `tm.therapist` | Writes and signs clinical notes |
| Assistant | `assistant` | `tm.assistant` | PT assistant: notes need a therapist co-sign |
| Scheduler | `scheduler` | `tm.scheduler` | Books and manages appointments |
| Biller | `biller` | `tm.biller` | Charges, claims, payments |
| Compliance | `compliance` | `tm.compliance` | Audit and compliance review |
| Patient | `patient` (Taylor Brooks) | `tm.patient` (Jordan Ellis) | Patient portal only |
| SuperAdmin | `superadmin` (platform-wide, belongs to no clinic) | | Manages all client clinics |

**Demo patients**
- Source Motion: Taylor Brooks, Riley Simmons, Harper Ellison, Quinn Alvarez
- Total Motion: Jordan Ellis, Reese Whitfield

**Public booking pages:** `source-motion-pt` and `total-motion-pt`

---

## 5. How to test with Swagger

1. Open http://localhost:5080/swagger.
2. Log in: expand **Auth → `POST /api/v1/auth/login`**, click **Try it out** and enter:
   ```json
   { "username": "admin", "password": "DemoPass123!" }
   ```
   Click **Execute**. You should get **200** and your user details.
   The browser now holds a login cookie, so every other request you send from this page runs as that user.
3. To switch user, call **`POST /api/v1/auth/logout`**, then log in again as someone else.
4. Many requests need an ID (a patient, note or appointment). Get one by first calling a
   "list" endpoint such as `GET /api/v1/patients` and copying an `id` from the response.

**How to read the response codes**

| Code | Meaning |
|---|---|
| 200 / 201 / 204 | Success |
| 400 | Invalid input. The response explains which field is wrong. |
| 401 | Not logged in |
| 403 | Logged in, but this role or clinic is not allowed to do this |
| 404 | Not found, or it belongs to another clinic |
| 409 | Conflict, for example a double-booked time slot |
| 500 | A bug. Always report it. |

---

## 6. Test scenarios

Each scenario lists **steps** and the **expected result**. Mark each one **Pass** or **Fail** and write down anything unexpected.

### 6.1 Login and sessions

| # | Steps | Expected |
|---|---|---|
| L1 | Web app: log in as `admin` / `DemoPass123!` | Lands on the home page with the navigation menu |
| L2 | Log in with a wrong password | Rejected with an error message; no crash |
| L3 | Log in using the email `admin@sourcemotionpt.test` | Works the same as the username |
| L4 | Open http://localhost:5073/patients without logging in | Redirected to the login page |
| L5 | Swagger: log in, then call `POST /auth/logout`, then `GET /patients` | Last call returns 401 |
| L6 | React app (http://localhost:5173): log in as `admin` | Dashboard page loads |

### 6.2 Patients (web app → **Patients**)

| # | Steps | Expected |
|---|---|---|
| P1 | Open **Patients** as `admin` | The 4 Source Motion patients are listed, and no Total Motion patients |
| P2 | Search for "Taylor" | Taylor Brooks is found |
| P3 | Open Taylor Brooks | Shows demographics, allergies, medications, diagnoses, insurance and notes |
| P4 | Click **New patient**, fill in first name, last name and date of birth, then **Save** | Patient is saved and its page opens |
| P5 | Click **New patient** and **Save** with the fields empty | "First and last name are required."; nothing saved |
| P6 | On a patient's page, click **Edit**, change phone, address and status, then **Save**; reload the page | Changes are shown and still there after reload |
| P6a | On a patient's page, click **Archive → Yes, archive** | Back on Patients; the patient is no longer listed |
| P6b | Tick **Show archived**, click **Restore** on that patient, then untick | Patient is back in the normal list |
| P6c | Log in as `biller` or `compliance` and open Patients and a patient page | No **New patient**, **Edit** or **Archive** buttons; `PUT /patients/{id}` in Swagger returns **403** |
| P7 | Swagger: `GET /patients/{id}/timeline` | History of the patient's activity is returned |

### 6.3 Scheduling (web app → **Schedule**, or Swagger → **Appointments**)

| # | Steps | Expected |
|---|---|---|
| S1 | Open **Schedule** as `scheduler` | Calendar with appointments loads |
| S2 | Book an appointment for a patient, provider and time | Appointment is created |
| S3 | Book a second appointment for the **same provider at the same time** | Rejected as a conflict |
| S4 | Move the appointment through its lifecycle: **confirm → check-in → complete** | Status changes at each step |
| S5 | Reschedule an appointment | New time saved; `GET /appointments/{id}/history` shows the change |
| S6 | Cancel one appointment and mark another **no-show** | Statuses update |
| S7 | Swagger: `POST /appointments/series` to book a recurring series, then cancel the series | All appointments in the series are created, then cancelled |
| S8 | Swagger: `GET /availability/slots` for a provider and date | Returns free time slots that do not overlap existing bookings |

### 6.3a Schedule page (React app → http://localhost:5173/schedule)

Demo data covers Sep 14 – Oct 11, 2026 at Source Motion (Jamie Chen PT, Sofia Ramirez PT — login `therapist2`, Avery Kim PTA — login `assistant`). Choose **All locations** to see every provider.

| # | Steps | Expected |
|---|---|---|
| SC1 | As `scheduler`, open **Day** view for a weekday | One column per PT/PTA with name, credentials, hours, and appointment count; off-hours are striped; lunch shows as a block; a summary card per provider below (appointments, completed, remaining, booked/available hours, utilization, next patient) |
| SC2 | Switch **Day → Week → Month → Year → List** | The date and filters are kept; Month shows a count per day and "+ X more"; clicking a day in Month or Year opens Day view |
| SC3 | Filter by provider, status, type, and patient name | Only matching appointments show; the URL keeps the filters |
| SC4 | Click an appointment | Details panel shows patient, DOB, MRN, phone, provider, time, type, location, status; only the actions valid for its status are offered |
| SC5 | **+ New appointment**: search an existing patient, pick provider/type/time, book | Booked; appears on the calendar |
| SC6 | New appointment: search a name that doesn't exist → **Create new patient** | Quick registration; the new patient is selected and can be booked. Registering the same name + DOB again offers the existing chart instead |
| SC7 | New appointment with **2x weekly** for 6 visits → **Check dates** | Shows how many can be booked and lists each conflicting date with the reason; **Book N appointments** books only the clean ones |
| SC8 | Drag an appointment to a new time in Day view | "Move appointment?" shows From and To; nothing changes until **Move appointment** |
| SC9 | Drag an appointment to another PT's column during their lunch | "Scheduling conflict" with the reason; `scheduler` sees only **Choose another time** |
| SC10 | Drag a **New** (Initial Evaluation) visit onto the PTA's column | Blocked — a PTA can't perform evaluations; no override offered |
| SC11 | As `admin`, drag a visit past a provider's end time | **Override and move** appears, disabled until a reason is typed; after moving, `AuditEvents` has `schedule.conflict_override` with the reason |
| SC12 | Book the **same patient** twice at overlapping times | Rejected: "This patient already has another appointment…" — never overridable |
| SC13 | **Provider hours**: add Saturday hours for a provider, save; open that Saturday in Day view | The column shows the new hours |
| SC14 | Provider hours: add time off over an existing visit | Saved; a warning lists the visits that now need rescheduling (they are not moved automatically) |
| SC15 | Log in as `therapist` | Schedule shows only their own column; Provider hours is read-only |
| SC16 | Log in as `patient` and call `GET /api/v1/schedule/day?date=…` in Swagger | **403** |
| SC17 | Open the Schedule page on a phone-sized window (≈390px wide) | Starts in **List** view; the sidebar is behind **☰ Menu**; no sideways scrolling |

### 6.4 Clinical notes (web app → patient → note, or Swagger → **Notes**)

| # | Steps | Expected |
|---|---|---|
| N1 | As `therapist`, open a patient and view their notes | Seeded notes are listed (Evaluation, Plan of Care, Daily, Discharge and others) |
| N2 | Create a new Daily note and save it as a draft | Saved as **Draft** |
| N3 | Edit the draft, then **sign** it | Status becomes **Signed** |
| N4 | Try to edit the signed note | Rejected; signed notes cannot be changed |
| N5 | Add an **addendum** to the signed note | Addendum is attached; the original text is unchanged |
| N6 | `GET /notes/{id}/versions` | Shows the version history |
| N7 | As `assistant`, write and sign a note; then as `therapist`, **co-sign** it | Co-signature is recorded |
| N8 | `GET /notes/patient/{patientId}/pull-forward` | Returns data from the previous note to pre-fill a new one |
| N9 | `GET /notes/patient/{patientId}/progress-note-status` | Shows whether a progress note is due |
| N10 | `POST /notes/{id}/certify-poc` on a Plan of Care note | Certification is recorded |
| N11 | Add a functional goal (Swagger → **Goals**) and update its progress | Goal and progress are saved |

### 6.5 Billing (web app → **Service prices**, **Payers**, claims; Swagger → Charges/Claims)

| # | Steps | Expected |
|---|---|---|
| B1 | As `biller`, open **Service prices** and **Payers** | Seeded prices and payer are listed |
| B2 | `POST /charges/generate-from-note/{noteId}` on a signed note with interventions | Charges are created with CPT codes and prices |
| B3 | Run B2 again on the same note | Rejected as a duplicate |
| B4 | `POST /charges/generate-from-appointment/{appointmentId}` on a **completed** appointment | Charge is created at the appointment type's price |
| B5 | `GET /charges/fee-schedule/resolve` for code 97110 at the Fuquay-Varina location, and again with no location | The location price is higher than the organization-wide price |
| B6 | Create a claim for a patient, then change its status (`PATCH /claims/{id}/status`) | Claim is created and the status changes; the claim page in the web app shows it |
| B7 | Record a payment (Swagger → **PaymentRecords** / **PatientPayments**) | Patient balance goes down |
| B8 | Swagger → **BillingReports**: run the aging and revenue reports | Reports return totals that match the data entered |
| B9 | Generate a patient statement and a superbill | Documents are produced |

### 6.6 Intake, consent and patient portal

| # | Steps | Expected |
|---|---|---|
| I1 | Log in as `patient`; call `GET /portal/me` | Returns Taylor Brooks' own information only |
| I2 | As `patient`: `GET /portal/intake-form-templates`, then `POST /portal/intake-form-submissions` | Intake form is submitted |
| I3 | As `patient`: `POST /portal/consents` | Consent is recorded |
| I4 | As `patient`, try a staff endpoint such as `GET /patients` | **403**. Patients must not see staff screens. |
| I5 | As `admin`, view the patient's intake submissions and consents | Items from I2 and I3 are visible |
| I6 | Share a document with the patient (Swagger → **SharedDocuments**) and view it as `patient` | Patient can see the shared document |
| I7 | Send and read messages (Swagger → **Messages**) | Messages appear for both staff and patient |
| I8 | Create a home exercise program for the patient | Program is saved and visible to the patient |

### 6.7 Public online booking (no login needed)

| # | Steps | Expected |
|---|---|---|
| PB1 | Log out, then call `GET /public/source-motion-pt` | Clinic details are returned |
| PB2 | Call `.../locations`, `.../appointment-types`, `.../providers`, `.../availability` | Each returns data |
| PB3 | `POST /public/source-motion-pt` with a valid booking | Booking request is created |
| PB4 | Use a slug that does not exist, e.g. `GET /public/not-a-clinic` | **404** |

### 6.8 Dashboards and reports

| # | Steps | Expected |
|---|---|---|
| D1 | As `director`, call each **Dashboards** endpoint (new patients, cancellations/no-shows, provider productivity, visits/retention, referral sources, location performance) | Each returns numbers for Source Motion only |
| D2 | As `therapist`, call the same endpoints | Blocked (403) if the role is not allowed to see reports |

### 6.9 Users and administration

| # | Steps | Expected |
|---|---|---|
| A1 | As `admin`, create a new user (Swagger → **Users**) | User is created with an invitation |
| A2 | Activate the invitation (`POST /auth/activate-invitation`) and log in as the new user | Login works |
| A3 | As `scheduler`, try to create a user | **403** |
| A4 | Add locations, rooms, providers and provider licenses | Saved; `GET /providers/licenses/expiring` lists licenses close to expiry |
| A5 | Change organization settings (Swagger → **Organizations**) | Saved |

### 6.10 SuperAdmin (platform administration)

Log in as `superadmin`.

| # | Steps | Expected |
|---|---|---|
| SA1 | `GET /super-admin/clients` | Both clinics are listed |
| SA2 | `POST /super-admin/clients` to create a new client clinic | Created with **Trial** status, a 14-day trial end date and a first location |
| SA3 | `PATCH /super-admin/clients/{clientNumber}/suspend` on Source Motion, then try to log in as `admin` | Login is refused with `ORGANIZATION_SUSPENDED` |
| SA4 | `PATCH .../activate` on the same clinic, then log in as `admin` again | Login works again |
| SA5 | `GET /super-admin/dashboards` | Cross-clinic statistics are returned |
| SA6 | Privileged access (Swagger → **PrivilegedAccess**): re-authenticate with a wrong password, then the right one | Wrong password is rejected; right password grants access |

### 6.11 Security: clinic isolation and roles (important)

These are the most important checks. A failure here is a **critical** bug.

| # | Steps | Expected |
|---|---|---|
| T1 | As `admin` (Source Motion), `GET /patients` | Only Source Motion patients |
| T2 | As `tm.admin`, copy a Total Motion patient's `id`. Log in as `admin` and call `GET /patients/{that id}` | **403 or 404**, never the patient's data |
| T3 | Repeat T2 for a note, an appointment and a claim | Same: blocked |
| T4 | As `admin`, try to update or delete a Total Motion record by ID | Blocked; the record is unchanged |
| T5 | As `biller`, try to sign a clinical note | Blocked |
| T6 | As `scheduler`, try to view billing reports | Blocked |
| T7 | After T2, check the `AuditEvents` table (see section 8) | An `access.denied` event is recorded |
| T8 | Force an error (e.g. send badly formed JSON) | The response does **not** contain a stack trace or patient details |

---

## 7. Automated tests

The project has an automated test suite of more than 330 tests. Run it from the project folder:

```powershell
dotnet test tests/PhysioTrac.Tests/PhysioTrac.Tests.csproj
```

Expected result: **all tests pass** (`Failed: 0`).

React app tests:
```powershell
cd frontend
npm test
```

The automated tests use an in-memory database, not SQL Server. Some problems
only show up against the real database, so run the manual tests above as well.

---

## 8. Checking the database

Use **SQL Server Management Studio (SSMS)**:

1. Connect to **Server name:** `.\SQLEXPRESS` with **Windows Authentication**, and check **Trust Server Certificate**.
2. Open **Databases → PhysioTrac → Tables**.
3. Right-click a table and choose **Select Top 1000 Rows**.

Useful tables:

| Table | What to check |
|---|---|
| `Patients` | Patients you created or archived |
| `Appointments` | Bookings and their status |
| `ClinicalNotes` | Note status (Draft or Signed) |
| `AuditEvents` | Logins, failed logins, `access.denied`, and created/updated/deleted records |
| `AspNetUsers` | User accounts (passwords are stored as hashes, not as readable text) |

API log files are written to `src/PhysioTrac.Api/logs/`.

---

## 9. Resetting to fresh demo data

If test data gets messy, start over:

1. Stop the API (Ctrl+C).
2. In SSMS, right-click **PhysioTrac → Delete**, check **Close existing connections**, and click **OK**.
3. Start the API again. It recreates the database and reloads all demo data.

---

## 10. Reporting a bug

Include:

- **Scenario number** (for example *N4*) and a short title
- **Account used** (for example `therapist`)
- **Steps to reproduce**
- **Expected result** and **actual result**
- **Screenshot**, or the Swagger response code and body
- The **time** it happened, so it can be matched in `src/PhysioTrac.Api/logs/`

Mark any failure in section 6.11 (security) or any **500** error as **high priority**.

Do not put real patient information into the test system or into bug reports. Use only the demo data.

import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiRequest } from "../../../lib/apiClient";
import { AdminPageHeader } from "../AdminPageHeader";
import { useAuth } from "../../auth/AuthProvider";
import { RoleSets, canAccess } from "../../auth/permissions";

// Matches DocumentationSettingsDto.
export interface DocumentationSettings {
  ptaCosignRequired: boolean;
  progressNoteDueVisitCount: number | null;
  progressNoteDueDays: number | null;
  autoCreatePendingCharges: boolean;
}

const KEY = ["organizations", "documentation-settings"];
const fetchSettings = () =>
  apiRequest<DocumentationSettings>(
    "/api/v1/organizations/documentation-settings",
  );
const saveSettings = (body: DocumentationSettings) =>
  apiRequest<DocumentationSettings>(
    "/api/v1/organizations/documentation-settings",
    { method: "PUT", body },
  );

const toNumber = (s: string) => (s.trim() === "" ? null : Number(s));

/** Clinic-wide documentation rules: PTA cosign, when a progress note is
 * due, and pending charges when a note is signed. */
export function DocumentationSettingsPage() {
  const settings = useQuery({ queryKey: KEY, queryFn: fetchSettings });
  return (
    <div className="mx-auto max-w-2xl">
      <AdminPageHeader title="Documentation Settings" />
      {settings.isError && (
        <p className="alert-error">{settings.error.message}</p>
      )}
      {settings.isLoading && <p className="text-text-muted">Loading…</p>}
      {settings.data && <SettingsForm initial={settings.data} />}
    </div>
  );
}

function SettingsForm({ initial }: { initial: DocumentationSettings }) {
  const { user } = useAuth();
  const canEdit = canAccess(user, RoleSets.OrganizationAdministration);
  const qc = useQueryClient();
  const [visits, setVisits] = useState(
    initial.progressNoteDueVisitCount?.toString() ?? "",
  );
  const [days, setDays] = useState(
    initial.progressNoteDueDays?.toString() ?? "",
  );
  const [cosign, setCosign] = useState(initial.ptaCosignRequired);
  const [charges, setCharges] = useState(initial.autoCreatePendingCharges);
  const [saved, setSaved] = useState(false);

  const visitsBad =
    visits.trim() !== "" &&
    !(Number.isInteger(Number(visits)) && +visits >= 1 && +visits <= 50);
  const daysBad =
    days.trim() !== "" &&
    !(Number.isInteger(Number(days)) && +days >= 1 && +days <= 365);

  const save = useMutation({
    mutationFn: () =>
      saveSettings({
        ptaCosignRequired: cosign,
        progressNoteDueVisitCount: toNumber(visits),
        progressNoteDueDays: toNumber(days),
        autoCreatePendingCharges: charges,
      }),
    onSuccess: (data) => {
      qc.setQueryData(KEY, data);
      setSaved(true);
    },
  });
  const serverErrors =
    save.error instanceof ApiError
      ? ((save.error.payload as { errors?: string[] } | undefined)?.errors ??
        [])
      : [];

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setSaved(false);
    if (!visitsBad && !daysBad) save.mutate();
  };

  return (
    <form onSubmit={onSubmit} className="card space-y-5">
      {saved && (
        <p
          role="status"
          className="rounded-md bg-success-light px-3 py-2 text-success"
        >
          Settings saved.
        </p>
      )}
      {save.isError && (
        <div role="alert" className="alert-error">
          {serverErrors.length ? serverErrors.join(" ") : save.error.message}
        </div>
      )}
      {!canEdit && (
        <p className="text-text-muted">
          Only administrators and directors can change these settings.
        </p>
      )}

      <fieldset className="space-y-3" disabled={!canEdit}>
        <legend className="text-xl font-bold text-[#1565b8]">
          Progress notes
        </legend>
        <p className="text-text-muted">
          A progress note is due at whichever comes first. Leave the visits box
          empty for the standard 10th visit, and the days box empty to turn that
          trigger off.
        </p>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="ds-visits" className="field-label">
              Treatment visits before a progress note is due
            </label>
            <input
              id="ds-visits"
              className="field-input"
              inputMode="numeric"
              placeholder="10"
              value={visits}
              aria-invalid={visitsBad}
              aria-describedby="ds-visits-help"
              onChange={(e) => setVisits(e.target.value)}
            />
            <p
              id="ds-visits-help"
              className={visitsBad ? "text-danger" : "text-text-muted"}
            >
              1 to 50. Empty uses the standard 10.
            </p>
          </div>
          <div>
            <label htmlFor="ds-days" className="field-label">
              Days before a progress note is due
            </label>
            <input
              id="ds-days"
              className="field-input"
              inputMode="numeric"
              value={days}
              aria-invalid={daysBad}
              aria-describedby="ds-days-help"
              onChange={(e) => setDays(e.target.value)}
            />
            <p
              id="ds-days-help"
              className={daysBad ? "text-danger" : "text-text-muted"}
            >
              1 to 365, counted from the evaluation or last progress note
              (commonly 30).
            </p>
          </div>
        </div>
      </fieldset>

      <fieldset className="space-y-3" disabled={!canEdit}>
        <legend className="text-xl font-bold text-[#1565b8]">Signing</legend>
        <label className="flex min-h-11 items-start gap-3">
          <input
            type="checkbox"
            className="mt-1 h-5 w-5"
            checked={cosign}
            onChange={(e) => setCosign(e.target.checked)}
          />
          <span>
            <span className="font-bold">
              Assistants’ notes need a PT cosignature
            </span>
            <span className="block text-text-muted">
              Applies to notes started after the change. Evaluations,
              re-evaluations, recertifications, progress notes and discharges
              written by an assistant always need one.
            </span>
          </span>
        </label>
        <label className="flex min-h-11 items-start gap-3">
          <input
            type="checkbox"
            className="mt-1 h-5 w-5"
            checked={charges}
            onChange={(e) => setCharges(e.target.checked)}
          />
          <span>
            <span className="font-bold">
              Create pending charges when a note is signed
            </span>
            <span className="block text-text-muted">
              Draft charges from the note’s billable interventions, for billing
              to review. Nothing is ever submitted automatically. An assistant’s
              note is charged when it is cosigned.
            </span>
          </span>
        </label>
      </fieldset>

      {canEdit && (
        <button
          type="submit"
          className="btn-primary"
          disabled={visitsBad || daysBad || save.isPending}
        >
          {save.isPending ? "Saving…" : "Save settings"}
        </button>
      )}
    </form>
  );
}

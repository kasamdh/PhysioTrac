import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RowMenu } from "../../../components/RowMenu";
import { useToast } from "../../../components/Toast";
import {
  addIntervention,
  deleteIntervention,
  fetchInterventionSummary,
  fetchInterventions,
  updateIntervention,
} from "../api";
import {
  InterventionCategoryLabels,
  type Intervention,
  type InterventionInput,
} from "../types";

const RULE_LABELS: Record<string, string> = {
  Medicare: "Medicare 8-minute rule, total timed minutes",
  RoundedFifteenMinute: "rounded 15-minute units, organization setting",
};

const blank = {
  description: "",
  bodyRegion: "",
  category: "0",
  minutes: "",
  isTimed: true,
  patientResponse: "",
};

/** Treatment provided this visit: what, where, how long, and how the patient
 * responded. Rows save immediately (they are their own records); the summary
 * shows the units the organization's 8-minute-rule variant gives. */
export function InterventionsPanel({
  noteId,
  readOnly,
}: {
  noteId: string;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [form, setForm] = useState(blank);
  const [editingId, setEditingId] = useState<string | null>(null);

  const items = useQuery({
    queryKey: ["chart", "interventions", noteId],
    queryFn: () => fetchInterventions(noteId),
  });
  const summary = useQuery({
    queryKey: ["chart", "intervention-summary", noteId],
    queryFn: () => fetchInterventionSummary(noteId),
  });
  const refresh = () => {
    void queryClient.invalidateQueries({
      queryKey: ["chart", "interventions", noteId],
    });
    void queryClient.invalidateQueries({
      queryKey: ["chart", "intervention-summary", noteId],
    });
  };

  const toInput = (): InterventionInput => ({
    description: form.description.trim(),
    bodyRegion: form.bodyRegion.trim() || null,
    category: form.category === "" ? null : Number(form.category),
    minutes: Number(form.minutes) || 0,
    units: null,
    isTimed: form.isTimed,
    order: editingId
      ? (items.data?.find((i) => i.id === editingId)?.order ?? 0)
      : (items.data?.length ?? 0),
    patientResponse: form.patientResponse.trim() || null,
  });

  const save = useMutation({
    mutationFn: () =>
      editingId
        ? updateIntervention(noteId, editingId, toInput())
        : addIntervention(noteId, toInput()),
    onSuccess: () => {
      setForm(blank);
      setEditingId(null);
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const remove = useMutation({
    mutationFn: (id: string) => deleteIntervention(noteId, id),
    onSuccess: refresh,
    onError: (e: Error) => showToast(e.message),
  });

  const startEdit = (i: Intervention) => {
    setEditingId(i.id);
    setForm({
      description: i.description,
      bodyRegion: i.bodyRegion ?? "",
      category: i.category === null ? "" : String(i.category),
      minutes: String(i.minutes),
      isTimed: i.isTimed,
      patientResponse: i.patientResponse ?? "",
    });
  };

  const minutesInvalid =
    form.minutes !== "" &&
    (Number(form.minutes) < 0 || Number(form.minutes) > 480);
  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (form.description.trim() && !minutesInvalid) save.mutate();
  };

  return (
    <div className="space-y-4">
      {summary.data && (items.data?.length ?? 0) > 0 && (
        <p className="rounded-md bg-primary-light px-3 py-2 text-[#333]">
          <strong>{summary.data.timedMinutes}</strong> timed minutes →{" "}
          <strong>
            {summary.data.estimatedTimedUnits} unit
            {summary.data.estimatedTimedUnits === 1 ? "" : "s"}
          </strong>{" "}
          <span className="text-text-muted">
            ({RULE_LABELS[summary.data.ruleVariant] ?? summary.data.ruleVariant}
            )
          </span>
          {summary.data.untimedCount > 0 && (
            <span className="text-text-muted">
              {" "}
              · {summary.data.untimedCount} untimed
            </span>
          )}
        </p>
      )}

      {!readOnly && (
        <form
          onSubmit={onSubmit}
          className="space-y-3 rounded-md border border-border p-3"
          aria-label={editingId ? "Edit intervention" : "Add intervention"}
        >
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_2fr_1fr_8rem]">
            <label className="text-[#333]">
              <span className="font-bold">Category</span>
              <select
                className="field-input mt-1"
                value={form.category}
                onChange={(e) => setForm({ ...form, category: e.target.value })}
              >
                {Object.entries(InterventionCategoryLabels).map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-[#333]">
              <span className="font-bold">What was done</span>
              <input
                className="field-input mt-1"
                placeholder="e.g. Bridges 3×10, STM lumbar paraspinals"
                value={form.description}
                onChange={(e) =>
                  setForm({ ...form, description: e.target.value })
                }
              />
            </label>
            <label className="text-[#333]">
              <span className="font-bold">Body region</span>
              <input
                className="field-input mt-1"
                placeholder="e.g. Lumbar"
                value={form.bodyRegion}
                onChange={(e) =>
                  setForm({ ...form, bodyRegion: e.target.value })
                }
              />
            </label>
            <label className="text-[#333]">
              <span className="font-bold">Minutes</span>
              <input
                inputMode="numeric"
                className="field-input mt-1"
                value={form.minutes}
                onChange={(e) =>
                  setForm({
                    ...form,
                    minutes: e.target.value.replace(/\D/g, ""),
                  })
                }
              />
            </label>
          </div>
          <label className="block text-[#333]">
            <span className="font-bold">Patient response</span>
            <input
              className="field-input mt-1"
              placeholder="e.g. Tolerated well, pain 4→2/10"
              value={form.patientResponse}
              onChange={(e) =>
                setForm({ ...form, patientResponse: e.target.value })
              }
            />
          </label>
          <div className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                className="h-5 w-5"
                checked={form.isTimed}
                onChange={(e) =>
                  setForm({ ...form, isTimed: e.target.checked })
                }
              />
              Timed service (counts toward units)
            </label>
            {minutesInvalid && (
              <span className="text-danger">Minutes must be 0–480.</span>
            )}
            <div className="ml-auto flex gap-2">
              {editingId && (
                <button
                  type="button"
                  className="btn-refresh"
                  onClick={() => {
                    setEditingId(null);
                    setForm(blank);
                  }}
                >
                  Cancel edit
                </button>
              )}
              <button
                type="submit"
                className="btn-primary"
                disabled={
                  !form.description.trim() || minutesInvalid || save.isPending
                }
              >
                {save.isPending
                  ? "Saving…"
                  : editingId
                    ? "Save intervention"
                    : "+ Add intervention"}
              </button>
            </div>
          </div>
        </form>
      )}

      {items.isLoading && <p className="text-text-muted">Loading…</p>}
      {items.data && items.data.length === 0 && (
        <p className="text-text-muted">
          {readOnly ? "None recorded." : "No interventions recorded yet."}
        </p>
      )}
      {items.data && items.data.length > 0 && (
        <div className="list-wrap">
          <table className="data-table">
            <thead>
              <tr>
                {!readOnly && (
                  <th className="cell-menu">
                    <span className="sr-only">Actions</span>
                  </th>
                )}
                <th>Category</th>
                <th>What was done</th>
                <th>Region</th>
                <th>Minutes</th>
                <th>Patient response</th>
              </tr>
            </thead>
            <tbody>
              {items.data.map((i) => (
                <tr key={i.id}>
                  {!readOnly && (
                    <td className="cell-menu">
                      <RowMenu
                        label={`Actions for ${i.description}`}
                        items={[
                          { label: "Edit", onSelect: () => startEdit(i) },
                          {
                            label: "Remove",
                            danger: true,
                            onSelect: () => remove.mutate(i.id),
                          },
                        ]}
                      />
                    </td>
                  )}
                  <td data-label="Category">
                    {i.category === null
                      ? "—"
                      : InterventionCategoryLabels[i.category]}
                  </td>
                  <td data-label="What was done">{i.description}</td>
                  <td data-label="Region">{i.bodyRegion ?? "—"}</td>
                  <td data-label="Minutes">
                    {i.minutes}
                    {!i.isTimed && (
                      <span className="text-text-muted"> (untimed)</span>
                    )}
                  </td>
                  <td data-label="Patient response">
                    {i.patientResponse ?? "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

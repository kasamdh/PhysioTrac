import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import {
  addAllergy,
  addMedication,
  addPatientDiagnosis,
  approveGoal,
  createGoal,
  fetchAllergies,
  fetchMedications,
  fetchPatientDiagnoses,
  fetchPlansOfCare,
  searchDiagnosisCodes,
} from "../api";

const formatDate = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};

/** The patient's ICD-10 diagnoses, with search-and-add while charting. */
export function DiagnosesPanel({
  patientId,
  readOnly,
}: {
  patientId: string;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [q, setQ] = useState("");
  const list = useQuery({
    queryKey: ["patient", patientId, "diagnoses"],
    queryFn: () => fetchPatientDiagnoses(patientId),
  });
  const search = useQuery({
    queryKey: ["diagnosis-codes", q.trim()],
    queryFn: () => searchDiagnosisCodes(q.trim()),
    enabled: !readOnly && q.trim().length >= 2,
  });
  const add = useMutation({
    mutationFn: (codeId: string) =>
      addPatientDiagnosis(
        patientId,
        codeId,
        !(list.data ?? []).some((d) => d.isPrimary && !d.isResolved),
      ),
    onSuccess: () => {
      setQ("");
      void queryClient.invalidateQueries({
        queryKey: ["patient", patientId, "diagnoses"],
      });
      void queryClient.invalidateQueries({ queryKey: ["encounter"] });
    },
    onError: (e: Error) => showToast(e.message),
  });
  const active = (list.data ?? []).filter((d) => !d.isResolved);
  return (
    <div className="space-y-2">
      {active.length === 0 ? (
        <p className="text-text-muted">No ICD-10 diagnoses on file.</p>
      ) : (
        <ul className="space-y-1">
          {active.map((d) => (
            <li key={d.id} className="text-[#333]">
              <strong>{d.code}</strong> {d.description}
              {d.isPrimary && (
                <span className="ml-2 text-text-muted">(primary)</span>
              )}
            </li>
          ))}
        </ul>
      )}
      {!readOnly && (
        <div>
          <label className="block max-w-xl">
            <span className="font-bold text-[#333]">Add ICD-10 code</span>
            <input
              type="search"
              className="field-input mt-1"
              placeholder="Search code or description, e.g. M25.561 or knee pain"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
          </label>
          {!!search.data?.length && (
            <ul
              className="mt-1 max-h-56 max-w-xl overflow-auto rounded-md border border-border"
              aria-label="Matching ICD-10 codes"
            >
              {search.data.map((c) => (
                <li key={c.id}>
                  <button
                    type="button"
                    className="w-full px-3 py-2 text-left hover:bg-surface-muted"
                    disabled={add.isPending}
                    onClick={() => add.mutate(c.id)}
                  >
                    <strong>{c.code}</strong> {c.description}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {search.data && search.data.length === 0 && q.trim().length >= 2 && (
            <p className="mt-1 text-text-muted">No matching codes.</p>
          )}
        </div>
      )}
    </div>
  );
}

/** Medications and allergies from the patient's record, with quick add. */
export function MedicalHistoryPanel({
  patientId,
  readOnly,
}: {
  patientId: string;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const meds = useQuery({
    queryKey: ["patient", patientId, "medications"],
    queryFn: () => fetchMedications(patientId),
  });
  const allergies = useQuery({
    queryKey: ["patient", patientId, "allergies"],
    queryFn: () => fetchAllergies(patientId),
  });
  const [med, setMed] = useState({ name: "", dosage: "", frequency: "" });
  const [allergy, setAllergy] = useState({ allergen: "", reaction: "" });
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["patient", patientId] });
    void queryClient.invalidateQueries({ queryKey: ["encounter"] });
  };
  const saveMed = useMutation({
    mutationFn: () =>
      addMedication(
        patientId,
        med.name.trim(),
        med.dosage.trim(),
        med.frequency.trim(),
      ),
    onSuccess: () => {
      setMed({ name: "", dosage: "", frequency: "" });
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const saveAllergy = useMutation({
    mutationFn: () =>
      addAllergy(patientId, allergy.allergen.trim(), allergy.reaction.trim()),
    onSuccess: () => {
      setAllergy({ allergen: "", reaction: "" });
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const submit = (e: FormEvent, run: () => void) => {
    e.preventDefault();
    run();
  };
  const activeMeds = (meds.data ?? []).filter((m) => m.isActive);
  const activeAllergies = (allergies.data ?? []).filter((a) => a.isActive);
  return (
    <div className="grid gap-4 md:grid-cols-2">
      <div>
        <p className="font-bold text-[#333]">Medications</p>
        {activeMeds.length === 0 ? (
          <p className="text-text-muted">None recorded.</p>
        ) : (
          <ul className="ml-5 list-disc text-[#333]">
            {activeMeds.map((m) => (
              <li key={m.id}>
                {[m.name, m.dosage, m.frequency].filter(Boolean).join(" · ")}
              </li>
            ))}
          </ul>
        )}
        {!readOnly && (
          <form
            aria-label="Add medication"
            className="mt-2 flex flex-wrap gap-2"
            onSubmit={(e) => submit(e, () => saveMed.mutate())}
          >
            <input
              aria-label="Medication"
              className="field-input max-w-[12rem]"
              placeholder="Medication"
              value={med.name}
              onChange={(e) => setMed({ ...med, name: e.target.value })}
            />
            <input
              aria-label="Dose"
              className="field-input max-w-[8rem]"
              placeholder="Dose"
              value={med.dosage}
              onChange={(e) => setMed({ ...med, dosage: e.target.value })}
            />
            <input
              aria-label="How often"
              className="field-input max-w-[8rem]"
              placeholder="How often"
              value={med.frequency}
              onChange={(e) => setMed({ ...med, frequency: e.target.value })}
            />
            <button
              type="submit"
              className="btn-refresh"
              disabled={!med.name.trim() || saveMed.isPending}
            >
              + Add
            </button>
          </form>
        )}
      </div>
      <div>
        <p className="font-bold text-[#333]">Allergies</p>
        {activeAllergies.length === 0 ? (
          <p className="text-text-muted">No known allergies recorded.</p>
        ) : (
          <ul className="ml-5 list-disc text-[#333]">
            {activeAllergies.map((a) => (
              <li key={a.id}>
                {a.allergen}
                {a.reaction ? ` — ${a.reaction}` : ""}
              </li>
            ))}
          </ul>
        )}
        {!readOnly && (
          <form
            aria-label="Add allergy"
            className="mt-2 flex flex-wrap gap-2"
            onSubmit={(e) => submit(e, () => saveAllergy.mutate())}
          >
            <input
              aria-label="Allergen"
              className="field-input max-w-[12rem]"
              placeholder="Allergen"
              value={allergy.allergen}
              onChange={(e) =>
                setAllergy({ ...allergy, allergen: e.target.value })
              }
            />
            <input
              aria-label="Reaction"
              className="field-input max-w-[12rem]"
              placeholder="Reaction"
              value={allergy.reaction}
              onChange={(e) =>
                setAllergy({ ...allergy, reaction: e.target.value })
              }
            />
            <button
              type="submit"
              className="btn-refresh"
              disabled={!allergy.allergen.trim() || saveAllergy.isPending}
            >
              + Add
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

export const PLAN_STATUS: Record<number, string> = {
  0: "Draft",
  1: "Active",
  2: "Superseded",
  3: "Discharged",
  4: "Expired",
};

/** The patient's current plan of care; a signed evaluation creates the next one. */
export function PlanOfCarePanel({
  patientId,
  createsPlan,
}: {
  patientId: string;
  createsPlan: boolean;
}) {
  const plans = useQuery({
    queryKey: ["patient", patientId, "plans-of-care"],
    queryFn: () => fetchPlansOfCare(patientId),
  });
  const active = (plans.data ?? []).find((p) => p.status === 1);
  return (
    <div className="rounded-md border border-border bg-surface-muted px-3 py-2 text-[#333]">
      {active ? (
        <>
          <p>
            <strong>Current plan of care:</strong>{" "}
            {formatDate(active.startDate)} – {formatDate(active.endDate)}
            {active.frequencyPerWeek
              ? ` · ${active.frequencyPerWeek}x/week`
              : ""}
            {active.durationWeeks ? ` for ${active.durationWeeks} weeks` : ""} (
            {PLAN_STATUS[active.status]})
          </p>
          {active.treatmentDiagnosis && (
            <p className="text-text-muted">{active.treatmentDiagnosis}</p>
          )}
        </>
      ) : (
        <p>No active plan of care.</p>
      )}
      {createsPlan && (
        <p className="mt-1 text-text-muted">
          Signing this note creates a new plan of care from the fields below
          {active ? "; the current one is kept as superseded." : "."}
        </p>
      )}
    </div>
  );
}

/** Create a functional, measurable goal (it starts as a draft for a PT to approve). */
export function AddGoalForm({ patientId }: { patientId: string }) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [open, setOpen] = useState(false);
  const [g, setG] = useState({
    term: "0",
    functionalTask: "",
    functionalLimitation: "",
    baselineValue: "",
    targetValue: "",
    unit: "",
    measurementMethod: "",
    targetDate: "",
  });
  const save = useMutation({
    mutationFn: () =>
      createGoal({
        patientId,
        term: Number(g.term),
        functionalTask: g.functionalTask.trim(),
        functionalLimitation: g.functionalLimitation.trim(),
        baselineValue: Number(g.baselineValue),
        targetValue: Number(g.targetValue),
        unit: g.unit.trim(),
        measurementMethod: g.measurementMethod.trim(),
        targetDate: g.targetDate,
        suggestedWording: null,
      }),
    onSuccess: () => {
      showToast("Goal added.");
      setOpen(false);
      setG({
        ...g,
        functionalTask: "",
        functionalLimitation: "",
        baselineValue: "",
        targetValue: "",
        measurementMethod: "",
        targetDate: "",
      });
      void queryClient.invalidateQueries({
        queryKey: ["chart", "goals", patientId],
      });
    },
    onError: (e: Error) => showToast(e.message),
  });
  const ready =
    g.functionalTask.trim() &&
    g.baselineValue !== "" &&
    g.targetValue !== "" &&
    g.unit.trim() &&
    g.targetDate;
  if (!open)
    return (
      <button
        type="button"
        className="btn-refresh"
        onClick={() => setOpen(true)}
      >
        + Add goal
      </button>
    );
  const field = (
    k: keyof typeof g,
    label: string,
    type = "text",
    placeholder = "",
  ) => (
    <label className="block">
      <span className="font-bold text-[#333]">{label}</span>
      <input
        type={type}
        inputMode={type === "number" ? "decimal" : undefined}
        className="field-input mt-1"
        placeholder={placeholder}
        value={g[k]}
        onChange={(e) => setG({ ...g, [k]: e.target.value })}
      />
    </label>
  );
  return (
    <form
      aria-label="Add goal"
      className="grid gap-3 rounded-md border border-border p-3 md:grid-cols-2"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <label className="block">
        <span className="font-bold text-[#333]">Term</span>
        <select
          className="field-input mt-1"
          value={g.term}
          onChange={(e) => setG({ ...g, term: e.target.value })}
        >
          <option value="0">Short-term</option>
          <option value="1">Long-term</option>
        </select>
      </label>
      {field("targetDate", "Target date", "date")}
      <div className="md:col-span-2">
        {field(
          "functionalTask",
          "Functional goal",
          "text",
          "e.g. Climb 12 stairs reciprocally with one rail",
        )}
      </div>
      <div className="md:col-span-2">
        {field(
          "functionalLimitation",
          "Functional limitation",
          "text",
          "e.g. Unable to climb stairs without pain",
        )}
      </div>
      {field("baselineValue", "Baseline", "number")}
      {field("targetValue", "Target", "number")}
      {field("unit", "Unit", "text", "e.g. stairs, sec, deg, points")}
      {field(
        "measurementMethod",
        "How it's measured",
        "text",
        "e.g. Stair count, LEFS",
      )}
      <div className="flex gap-2 md:col-span-2">
        <button
          type="submit"
          className="btn-primary"
          disabled={!ready || save.isPending}
        >
          {save.isPending ? "Saving…" : "Save goal"}
        </button>
        <button
          type="button"
          className="btn-refresh"
          onClick={() => setOpen(false)}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

/** Approve a draft goal (a PT's step). */
export function ApproveGoalButton({
  goalId,
  patientId,
}: {
  goalId: string;
  patientId: string;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const approve = useMutation({
    mutationFn: () => approveGoal(goalId),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: ["chart", "goals", patientId],
      }),
    onError: (e: Error) => showToast(e.message),
  });
  return (
    <button
      type="button"
      className="btn-refresh"
      disabled={approve.isPending}
      onClick={() => approve.mutate()}
    >
      Approve goal
    </button>
  );
}

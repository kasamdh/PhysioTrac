import { useState, type FormEvent, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useToast } from "../../components/Toast";
import {
  createExercise,
  exercisesKey,
  fetchExercise,
  setExerciseActive,
  updateExercise,
} from "./api";
import { ExerciseImageManager } from "./ExerciseImageManager";
import {
  BodyRegionLabels,
  CategoryLabels,
  DifficultyLabels,
  LateralityLabels,
  PositionLabels,
  type Exercise,
  type ExerciseInput,
} from "./types";

const EMPTY: ExerciseInput = {
  name: "",
  patientDescription: null,
  clinicalPurpose: null,
  bodyRegion: 6,
  targetMuscles: null,
  category: 13,
  startingPosition: null,
  instructions: null,
  endingPosition: null,
  equipment: null,
  difficulty: 0,
  position: 1,
  laterality: 0,
  breathingInstructions: null,
  commonMistakes: null,
  safetyPrecautions: null,
  contraindications: null,
  progressions: null,
  regressions: null,
  videoUrl: null,
};

const pick = (x: Exercise): ExerciseInput => {
  const input = { ...EMPTY };
  for (const k of Object.keys(EMPTY) as (keyof ExerciseInput)[])
    (input as Record<string, unknown>)[k] = x[k];
  return input;
};

/** Add or edit an exercise; once saved, its images are managed below. */
export function ExerciseEditorPage() {
  const { exerciseId } = useParams();
  const existing = useQuery({
    queryKey: [...exercisesKey, exerciseId],
    queryFn: () => fetchExercise(exerciseId!),
    enabled: !!exerciseId,
  });
  if (exerciseId && existing.isLoading)
    return <p className="text-text-muted">Loading…</p>;
  if (existing.isError)
    return <p className="alert-error">{existing.error.message}</p>;
  return (
    <ExerciseForm
      key={existing.data?.id ?? "new"}
      existing={existing.data ?? null}
    />
  );
}

function ExerciseForm({ existing }: { existing: Exercise | null }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [form, setForm] = useState<ExerciseInput>(
    existing ? pick(existing) : EMPTY,
  );
  const set = <K extends keyof ExerciseInput>(k: K, v: ExerciseInput[K]) =>
    setForm((f) => ({ ...f, [k]: v }));
  const readOnly = !!existing && !existing.canEdit;

  const save = useMutation({
    mutationFn: () => {
      const body = Object.fromEntries(
        Object.entries(form).map(([k, v]) => [
          k,
          typeof v === "string" && v.trim() === "" ? null : v,
        ]),
      ) as ExerciseInput;
      return existing
        ? updateExercise(existing.id, body)
        : createExercise(body);
    },
    onSuccess: (x) => {
      queryClient.setQueryData([...exercisesKey, x.id], x);
      void queryClient.invalidateQueries({
        queryKey: [...exercisesKey, "search"],
      });
      showToast(
        `${x.name} ${existing ? "saved" : "added. Now add its images."}`,
      );
      if (!existing) navigate(`/exercises/${x.id}/edit`, { replace: true });
    },
  });
  const toggle = useMutation({
    mutationFn: () => setExerciseActive(existing!.id, !existing!.isActive),
    onSuccess: (x) => {
      queryClient.setQueryData([...exercisesKey, x.id], x);
      void queryClient.invalidateQueries({
        queryKey: [...exercisesKey, "search"],
      });
      showToast(`${x.name} ${x.isActive ? "reactivated" : "deactivated"}.`);
    },
  });

  const nameMissing = !form.name.trim();
  const videoBad =
    !!form.videoUrl?.trim() && !/^https:\/\/\S+$/i.test(form.videoUrl.trim());
  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!nameMissing && !videoBad) save.mutate();
  };

  const text = (
    k: keyof ExerciseInput,
    label: string,
    help?: string,
    rows = 3,
  ) => (
    <Field label={label} htmlFor={`ex-${k}`} help={help}>
      <textarea
        id={`ex-${k}`}
        className="field-input"
        rows={rows}
        readOnly={readOnly}
        value={(form[k] as string | null) ?? ""}
        onChange={(e) => set(k, e.target.value as never)}
      />
    </Field>
  );
  const select = (
    k: "bodyRegion" | "category" | "difficulty" | "position" | "laterality",
    label: string,
    labels: Record<number, string>,
  ) => (
    <Field label={label} htmlFor={`ex-${k}`}>
      <select
        id={`ex-${k}`}
        className="field-input"
        disabled={readOnly}
        value={form[k]}
        onChange={(e) => set(k, Number(e.target.value))}
      >
        {Object.entries(labels).map(([v, l]) => (
          <option key={v} value={v}>
            {l}
          </option>
        ))}
      </select>
    </Field>
  );

  return (
    <div className="mx-auto max-w-4xl space-y-5 pb-10">
      <div>
        <Link to="/exercises" className="text-primary hover:underline">
          ‹ Exercise Library
        </Link>
        <h1 className="mt-1 text-2xl font-bold text-[#1565b8]">
          {existing ? `Edit: ${existing.name}` : "Add exercise"}
        </h1>
        {existing?.isPlatform && (
          <p className="text-text-muted">
            Platform exercise — shared with every clinic
            {readOnly ? "; only the platform can change it." : "."}
          </p>
        )}
      </div>

      <form onSubmit={onSubmit} noValidate className="card space-y-4">
        {save.isError && (
          <p role="alert" className="alert-error">
            {save.error.message}
          </p>
        )}
        <Field label="Name" htmlFor="ex-name" missing={nameMissing}>
          <input
            id="ex-name"
            className="field-input"
            readOnly={readOnly}
            value={form.name}
            onChange={(e) => set("name", e.target.value)}
          />
        </Field>
        {text(
          "patientDescription",
          "Patient-friendly description",
          "Shown to the patient.",
          2,
        )}
        <div className="grid gap-4 sm:grid-cols-2">
          {select("bodyRegion", "Body region", BodyRegionLabels)}
          {select("category", "Category", CategoryLabels)}
          {select("difficulty", "Difficulty", DifficultyLabels)}
          {select("position", "Position", PositionLabels)}
          {select("laterality", "Side", LateralityLabels)}
          <Field label="Equipment" htmlFor="ex-equipment">
            <input
              id="ex-equipment"
              className="field-input"
              readOnly={readOnly}
              value={form.equipment ?? ""}
              onChange={(e) => set("equipment", e.target.value)}
            />
          </Field>
        </div>
        <Field label="Target muscles" htmlFor="ex-targetMuscles">
          <input
            id="ex-targetMuscles"
            className="field-input"
            readOnly={readOnly}
            value={form.targetMuscles ?? ""}
            onChange={(e) => set("targetMuscles", e.target.value)}
          />
        </Field>
        {text("clinicalPurpose", "Clinical purpose", "For clinicians.", 2)}
        {text("startingPosition", "Starting position", undefined, 2)}
        {text(
          "instructions",
          "Step-by-step instructions",
          "One step per line.",
          5,
        )}
        {text("endingPosition", "Ending position", undefined, 2)}
        {text("breathingInstructions", "Breathing", undefined, 2)}
        <div className="grid gap-4 sm:grid-cols-2">
          {text("safetyPrecautions", "Safety precautions")}
          {text("contraindications", "Contraindications")}
          {text("commonMistakes", "Common mistakes")}
          {text("progressions", "Progressions")}
          {text("regressions", "Regressions")}
        </div>
        <Field
          label="Instructional video (optional)"
          htmlFor="ex-videoUrl"
          help={videoBad ? undefined : "An https:// link."}
        >
          <input
            id="ex-videoUrl"
            className="field-input"
            readOnly={readOnly}
            value={form.videoUrl ?? ""}
            aria-invalid={videoBad}
            onChange={(e) => set("videoUrl", e.target.value)}
          />
          {videoBad && (
            <p className="mt-1 text-danger">
              The link must start with https://
            </p>
          )}
        </Field>
        {!readOnly && (
          <div className="flex flex-wrap gap-3">
            <button
              type="submit"
              className="btn-primary"
              disabled={nameMissing || videoBad || save.isPending}
            >
              {save.isPending
                ? "Saving…"
                : existing
                  ? "Save exercise"
                  : "Add exercise"}
            </button>
            {existing && (
              <button
                type="button"
                className="btn-refresh"
                disabled={toggle.isPending}
                onClick={() => {
                  if (
                    existing.isActive &&
                    !window.confirm(
                      `Deactivate ${existing.name}? It will no longer be offered for new programs.`,
                    )
                  )
                    return;
                  toggle.mutate();
                }}
              >
                {existing.isActive ? "Deactivate" : "Reactivate"}
              </button>
            )}
          </div>
        )}
      </form>

      {existing && <ExerciseImageManager exercise={existing} />}
    </div>
  );
}

function Field({
  label,
  htmlFor,
  help,
  missing,
  children,
}: {
  label: string;
  htmlFor: string;
  help?: string;
  missing?: boolean;
  children: ReactNode;
}) {
  return (
    <div>
      <label
        htmlFor={htmlFor}
        className={`field-label ${missing ? "text-[#7a1c1c]" : ""}`}
      >
        {label}
      </label>
      {children}
      {missing && <p className="mt-1 text-[#7a1c1c]">Required</p>}
      {help && <p className="mt-1 text-text-muted">{help}</p>}
    </div>
  );
}

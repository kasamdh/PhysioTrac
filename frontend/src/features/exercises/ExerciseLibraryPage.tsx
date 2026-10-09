import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { RoleSets, canAccess } from "../auth/permissions";
import { exercisesKey, searchExercises } from "./api";
import { ExerciseDetailDialog } from "./ExerciseDetailDialog";
import { ExercisePicture } from "./ExercisePicture";
import {
  BodyRegionLabels,
  CategoryLabels,
  DifficultyLabels,
  type ExerciseSummary,
} from "./types";

const options = (labels: Record<number, string>) =>
  Object.entries(labels).map(([v, l]) => (
    <option key={v} value={v}>
      {l}
    </option>
  ));

const num = (v: string) => (v === "" ? null : Number(v));

/** The Home Exercise Program exercise library: search and filter, cards
 * with images, and the full exercise in a dialog (?exercise=id). */
export function ExerciseLibraryPage() {
  const { user } = useAuth();
  const canManage = canAccess(user, RoleSets.ExerciseLibrary);
  const [params, setParams] = useSearchParams();
  const [q, setQ] = useState("");
  const [equipment, setEquipment] = useState("");
  const [bodyRegion, setBodyRegion] = useState("");
  const [category, setCategory] = useState("");
  const [difficulty, setDifficulty] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const filters = {
    q: useDeferredValue(q),
    equipment: useDeferredValue(equipment),
    bodyRegion: num(bodyRegion),
    category: num(category),
    difficulty: num(difficulty),
    includeInactive,
  };
  const exercises = useQuery({
    queryKey: [...exercisesKey, "search", filters],
    queryFn: () => searchExercises(filters),
  });
  const openId = params.get("exercise");
  const setOpen = (id: string | null) =>
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (id) next.set("exercise", id);
        else next.delete("exercise");
        return next;
      },
      { replace: true },
    );
  const anyFilter = q || equipment || bodyRegion || category || difficulty;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold text-[#1565b8]">Exercise Library</h1>
        {canManage && (
          <Link to="/exercises/new" className="btn-primary">
            + Add exercise
          </Link>
        )}
      </div>

      <div className="list-toolbar">
        <input
          type="search"
          aria-label="Search exercises"
          className="toolbar-select w-full sm:w-64"
          placeholder="Search name, muscle, equipment"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <label className="toolbar-label">
          Body region
          <select
            className="toolbar-select"
            value={bodyRegion}
            onChange={(e) => setBodyRegion(e.target.value)}
          >
            <option value="">All</option>
            {options(BodyRegionLabels)}
          </select>
        </label>
        <label className="toolbar-label">
          Category
          <select
            className="toolbar-select"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          >
            <option value="">All</option>
            {options(CategoryLabels)}
          </select>
        </label>
        <label className="toolbar-label">
          Difficulty
          <select
            className="toolbar-select"
            value={difficulty}
            onChange={(e) => setDifficulty(e.target.value)}
          >
            <option value="">All</option>
            {options(DifficultyLabels)}
          </select>
        </label>
        <label className="toolbar-label">
          Equipment
          <input
            className="toolbar-select w-36"
            placeholder="e.g. band"
            value={equipment}
            onChange={(e) => setEquipment(e.target.value)}
          />
        </label>
        {canManage && (
          <label className="flex min-h-11 items-center gap-2 font-bold text-[#333]">
            <input
              type="checkbox"
              className="h-5 w-5"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            Show inactive
          </label>
        )}
        {anyFilter && (
          <button
            type="button"
            className="btn-refresh"
            onClick={() => {
              setQ("");
              setEquipment("");
              setBodyRegion("");
              setCategory("");
              setDifficulty("");
            }}
          >
            Clear filters
          </button>
        )}
      </div>

      {exercises.isLoading && <p className="text-text-muted">Loading…</p>}
      {exercises.isError && (
        <p className="alert-error">{exercises.error.message}</p>
      )}
      {exercises.data && exercises.data.length === 0 && (
        <p className="text-text-muted">No exercises match these filters.</p>
      )}
      {exercises.data && exercises.data.length > 0 && (
        <>
          <p className="mb-2 text-text-muted" aria-live="polite">
            {exercises.data.length} exercise
            {exercises.data.length === 1 ? "" : "s"}
          </p>
          <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {exercises.data.map((x) => (
              <ExerciseCard
                key={x.id}
                exercise={x}
                onOpen={() => setOpen(x.id)}
              />
            ))}
          </ul>
        </>
      )}

      {openId && (
        <ExerciseDetailDialog
          exerciseId={openId}
          onClose={() => setOpen(null)}
        />
      )}
    </div>
  );
}

export function ExerciseCard({
  exercise: x,
  onOpen,
  onAdd,
}: {
  exercise: ExerciseSummary;
  onOpen: () => void;
  onAdd?: () => void;
}) {
  return (
    <li
      className={`flex flex-col overflow-hidden rounded-lg border border-border bg-white ${x.isActive ? "" : "opacity-60"}`}
    >
      <ExercisePicture
        exerciseId={x.id}
        image={x.coverImage}
        thumbnail
        className="rounded-none border-0 border-b"
      />
      <div className="flex flex-1 flex-col gap-1 p-3">
        <h2 className="text-lg font-bold text-[#333]">{x.name}</h2>
        <p className="text-text-muted">
          {BodyRegionLabels[x.bodyRegion]} · {DifficultyLabels[x.difficulty]}
          {x.imageCount > 1 ? ` · ${x.imageCount} steps` : ""}
        </p>
        <p className="text-text-muted">
          {x.equipment ? `Equipment: ${x.equipment}` : "No equipment"}
        </p>
        {x.patientDescription && (
          <p className="line-clamp-2 text-[#333]">{x.patientDescription}</p>
        )}
        <div className="flex flex-wrap gap-1">
          {x.needsClinicalReview && (
            <span className="rounded-full bg-warning-light px-2 text-sm text-[#333]">
              Needs clinical review
            </span>
          )}
          {!x.isPlatform && (
            <span className="rounded-full bg-primary-light px-2 text-sm text-[#333]">
              Clinic
            </span>
          )}
          {!x.isActive && (
            <span className="rounded-full bg-surface-muted px-2 text-sm text-[#333]">
              Inactive
            </span>
          )}
        </div>
        <div className="mt-auto flex flex-wrap gap-2 pt-2">
          <button
            type="button"
            className="btn-refresh"
            onClick={onOpen}
            aria-label={`View details: ${x.name}`}
          >
            View details
          </button>
          {onAdd && (
            <button
              type="button"
              className="btn-primary"
              onClick={onAdd}
              disabled={!x.isActive}
              aria-label={`Add to HEP: ${x.name}`}
            >
              Add to HEP
            </button>
          )}
        </div>
      </div>
    </li>
  );
}

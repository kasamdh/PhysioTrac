import { useEffect, useId, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { exercisesKey, fetchExercise, markExerciseReviewed } from "./api";
import { ExercisePicture } from "./ExercisePicture";
import {
  BodyRegionLabels,
  CategoryLabels,
  DifficultyLabels,
  LateralityLabels,
  PositionLabels,
  stepLabel,
  type Exercise,
} from "./types";

/** An exercise in full: the large image and its numbered sequence, the
 * step-by-step instructions, and the safety information. `onAdd` shows an
 * "Add to HEP" button (the program builder passes it). */
export function ExerciseDetailDialog({
  exerciseId,
  onClose,
  onAdd,
}: {
  exerciseId: string;
  onClose: () => void;
  onAdd?: (exercise: Exercise) => void;
}) {
  const titleId = useId();
  const queryClient = useQueryClient();
  const exercise = useQuery({
    queryKey: [...exercisesKey, exerciseId],
    queryFn: () => fetchExercise(exerciseId),
  });
  const [step, setStep] = useState(0);
  const review = useMutation({
    mutationFn: () => markExerciseReviewed(exerciseId),
    onSuccess: (x) => {
      queryClient.setQueryData([...exercisesKey, exerciseId], x);
      void queryClient.invalidateQueries({ queryKey: exercisesKey });
    },
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  const x = exercise.data;
  const images = x?.images ?? [];
  const current = images[Math.min(step, Math.max(images.length - 1, 0))];

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[92vh] w-full max-w-4xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl"
      >
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-border px-6 py-4">
          <div>
            <h2 id={titleId} className="text-3xl text-[#333]">
              {x?.name ?? "Exercise"}
            </h2>
            {x && (
              <p className="text-text-muted">
                {BodyRegionLabels[x.bodyRegion]} · {CategoryLabels[x.category]}{" "}
                · {DifficultyLabels[x.difficulty]}
                {x.isPlatform ? " · Platform exercise" : " · Clinic exercise"}
              </p>
            )}
          </div>
          <button type="button" className="btn-refresh" onClick={onClose}>
            Close
          </button>
        </div>

        <div className="overflow-y-auto px-6 py-5">
          {exercise.isLoading && <p className="text-text-muted">Loading…</p>}
          {exercise.isError && (
            <p className="alert-error">{exercise.error.message}</p>
          )}
          {x && (
            <div className="space-y-5">
              {x.needsClinicalReview && (
                <div className="flex flex-wrap items-center gap-3 rounded-md border border-warning bg-warning-light px-3 py-2 text-[#333]">
                  <span>
                    <strong>Needs clinical review.</strong> This starter content
                    hasn’t been confirmed by a clinician yet.
                  </span>
                  {x.canEdit && (
                    <button
                      type="button"
                      className="btn-refresh"
                      disabled={review.isPending}
                      onClick={() => review.mutate()}
                    >
                      Mark as reviewed
                    </button>
                  )}
                </div>
              )}
              {review.isError && (
                <p className="alert-error">{review.error.message}</p>
              )}

              <div className="grid gap-5 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
                <div className="space-y-3">
                  <ExercisePicture exerciseId={x.id} image={current} />
                  {current && images.length > 1 && (
                    <p className="font-bold text-[#333]">
                      {stepLabel(current, images.length)}
                    </p>
                  )}
                  {images.length > 1 && (
                    <ol
                      aria-label="Image sequence"
                      className="grid grid-cols-3 gap-2 sm:grid-cols-4"
                    >
                      {images.map((img, i) => (
                        <li key={img.id}>
                          <button
                            type="button"
                            aria-label={`Show ${stepLabel(img, images.length)}`}
                            aria-current={i === step ? "step" : undefined}
                            onClick={() => setStep(i)}
                            className={`relative block w-full rounded border-2 ${i === step ? "border-primary" : "border-transparent"}`}
                          >
                            <span className="absolute top-1 left-1 rounded-full bg-[#1565b8] px-2 text-sm font-bold text-white">
                              {img.sequence}
                            </span>
                            <ExercisePicture
                              exerciseId={x.id}
                              image={img}
                              thumbnail
                              decorative
                            />
                          </button>
                        </li>
                      ))}
                    </ol>
                  )}
                  {current?.sourceAttribution && (
                    <p className="text-sm text-text-muted">
                      Image: {current.sourceAttribution}
                    </p>
                  )}
                </div>

                <div className="space-y-4 text-[#333]">
                  {x.patientDescription && (
                    <p className="text-lg">{x.patientDescription}</p>
                  )}
                  <dl className="grid grid-cols-2 gap-x-4 gap-y-1">
                    <Fact label="Position" value={PositionLabels[x.position]} />
                    <Fact label="Side" value={LateralityLabels[x.laterality]} />
                    <Fact label="Equipment" value={x.equipment ?? "None"} />
                    <Fact
                      label="Target muscles"
                      value={x.targetMuscles ?? "—"}
                    />
                  </dl>
                  <Block title="Starting position">{x.startingPosition}</Block>
                  {x.instructions && (
                    <section>
                      <h3 className="font-bold text-[#1565b8]">Instructions</h3>
                      <ol className="list-decimal space-y-1 pl-6">
                        {x.instructions
                          .split("\n")
                          .filter((l) => l.trim())
                          .map((l, i) => (
                            <li key={i}>{l}</li>
                          ))}
                      </ol>
                    </section>
                  )}
                  <Block title="Ending position">{x.endingPosition}</Block>
                  <Block title="Breathing">{x.breathingInstructions}</Block>
                </div>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Block title="Safety precautions" tone="danger">
                  {x.safetyPrecautions}
                </Block>
                <Block title="Contraindications" tone="danger">
                  {x.contraindications}
                </Block>
                <Block title="Common mistakes">{x.commonMistakes}</Block>
                <Block title="Clinical purpose">{x.clinicalPurpose}</Block>
                <Block title="Progressions">{x.progressions}</Block>
                <Block title="Regressions">{x.regressions}</Block>
              </div>
              {x.videoUrl && (
                <p>
                  <a
                    href={x.videoUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="table-link"
                  >
                    Instructional video (opens in a new tab)
                  </a>
                </p>
              )}
              <p className="text-sm text-text-muted">
                {x.reviewedAt &&
                  `Reviewed by ${x.reviewedByName ?? "a clinician"} on ${new Date(x.reviewedAt).toLocaleDateString("en-US")}. `}
                Last updated {new Date(x.updatedAt).toLocaleDateString("en-US")}
                {x.updatedByName ? ` by ${x.updatedByName}` : ""}.
              </p>
            </div>
          )}
        </div>

        {x && (x.canEdit || onAdd) && (
          <div className="flex flex-wrap justify-end gap-3 border-t border-border px-6 py-4">
            {x.canEdit && (
              <Link to={`/exercises/${x.id}/edit`} className="btn-refresh">
                Edit exercise and images
              </Link>
            )}
            {onAdd && (
              <button
                type="button"
                className="btn-primary"
                disabled={!x.isActive}
                onClick={() => onAdd(x)}
              >
                Add to HEP
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-text-muted">{label}</dt>
      <dd className="font-bold">{value}</dd>
    </div>
  );
}

function Block({
  title,
  children,
  tone,
}: {
  title: string;
  children: ReactNode;
  tone?: "danger";
}) {
  if (!children) return null;
  return (
    <section>
      <h3
        className={`font-bold ${tone === "danger" ? "text-danger" : "text-[#1565b8]"}`}
      >
        {title}
      </h3>
      <p className="whitespace-pre-line">{children}</p>
    </section>
  );
}

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import {
  fetchEpisodeSummary,
  prefillNote,
  reviewPrefill,
  type EpisodeSummary,
} from "./api";

const formatDate = (iso: string | null) => {
  if (!iso) return "—";
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};

/** For progress notes, re-evaluations, recertifications and discharge
 * summaries: what the patient's signed charting shows for the episode,
 * a button that fills the note's empty fields from it, and the review the
 * therapist must confirm before the note can be signed. */
export function EpisodePanel({
  noteId,
  readOnly,
  saveVersion,
  busy,
  prefilledAt,
  prefillReviewedAt,
  onFilled,
}: {
  noteId: string;
  readOnly: boolean;
  saveVersion: number;
  /** Unsaved or saving changes: fill only from a saved note. */
  busy: boolean;
  prefilledAt: string | null | undefined;
  prefillReviewedAt: string | null | undefined;
  onFilled: () => void;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [reviewedAt, setReviewedAt] = useState(prefillReviewedAt ?? null);
  const summary = useQuery({
    queryKey: ["episode-summary", noteId],
    queryFn: () => fetchEpisodeSummary(noteId),
  });
  const fill = useMutation({
    mutationFn: () => prefillNote(noteId, saveVersion),
    onSuccess: (r) => {
      const parts = [
        r.filledFields.length
          ? `Filled ${r.filledFields.length} field${r.filledFields.length === 1 ? "" : "s"}`
          : null,
        r.goalsAdded
          ? `added ${r.goalsAdded} goal${r.goalsAdded === 1 ? "" : "s"}`
          : null,
      ].filter(Boolean);
      showToast(
        parts.length
          ? `${parts.join(" and ")} from signed charting. Review before signing.`
          : "Nothing to fill — those fields already have content.",
      );
      if (parts.length) onFilled();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const review = useMutation({
    mutationFn: () => reviewPrefill(noteId),
    onSuccess: (n) => {
      setReviewedAt(n.prefillReviewedAt ?? new Date().toISOString());
      void queryClient.invalidateQueries({
        queryKey: ["chart", "compliance", noteId],
      });
    },
    onError: (e: Error) => showToast(e.message),
  });

  const s = summary.data;
  return (
    <section
      aria-labelledby="episode-title"
      className="mb-4 rounded-lg border border-border bg-white p-4"
    >
      <h2 id="episode-title" className="text-xl font-bold text-primary">
        From the patient’s signed charting
      </h2>
      {summary.isLoading && <p className="text-text-muted">Loading…</p>}
      {summary.error && <p className="alert-error">{summary.error.message}</p>}
      {s && <SummaryFacts s={s} />}

      {!readOnly && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="btn-primary"
            disabled={busy || fill.isPending || !s}
            onClick={() => fill.mutate()}
          >
            {fill.isPending ? "Filling…" : "Fill empty fields from charting"}
          </button>
          <span className="text-text-muted">
            {busy
              ? "Saving your changes first…"
              : "Only empty fields are filled; nothing you wrote is replaced."}
          </span>
        </div>
      )}

      {prefilledAt && !reviewedAt && (
        <div
          role="alert"
          className="mt-3 rounded-md border border-warning bg-warning-light px-3 py-2 text-[#333]"
        >
          <p>
            <strong>Review the pre-filled content.</strong> Fields were filled
            from signed charting on {formatDate(prefilledAt)}. Check and edit
            each one — the note can’t be signed until you confirm.
          </p>
          {!readOnly && (
            <button
              type="button"
              className="btn-primary mt-2"
              disabled={review.isPending || busy}
              onClick={() => review.mutate()}
            >
              {review.isPending
                ? "Confirming…"
                : "I have reviewed the pre-filled content"}
            </button>
          )}
        </div>
      )}
      {prefilledAt && reviewedAt && (
        <p className="mt-3 text-success">
          Pre-filled content reviewed {formatDate(reviewedAt)}.
        </p>
      )}
    </section>
  );
}

function SummaryFacts({ s }: { s: EpisodeSummary }) {
  const lists: [string, string[]][] = [
    ["Pain", s.painSummary ? [s.painSummary] : []],
    ["Objective measurements", s.measurementChanges],
    ["Outcome measures", s.outcomeChanges],
    ["Goals", s.goalLines],
    [
      s.evaluationDate
        ? `Since the evaluation of ${formatDate(s.evaluationDate)}`
        : "Since the evaluation",
      s.noteType === 5 || s.noteType === 11 ? s.sinceEvaluation : [],
    ],
    [
      `Since the progress note of ${formatDate(s.previousProgressDate)}`,
      s.noteType !== 4 ? s.sinceProgress : [],
    ],
    ["Home program", s.homeProgram ? s.homeProgram.split("\n") : []],
  ];
  return (
    <>
      <dl className="mt-2 grid gap-x-6 gap-y-2 sm:grid-cols-2 lg:grid-cols-4">
        <Fact
          label="Period"
          value={`${formatDate(s.periodStart)} – ${formatDate(s.periodEnd)}`}
          detail={s.periodBasis}
        />
        <Fact
          label="Signed visits"
          value={
            s.noteType === 4
              ? `${s.visitsInPeriod} this period`
              : `${s.visitsInEpisode} this episode`
          }
          detail={
            s.noteType === 4 ? `${s.visitsInEpisode} this episode` : undefined
          }
        />
        <Fact label="Attendance" value={s.attendance.text} />
        <Fact
          label="Plan of care"
          value={
            s.planStart
              ? `${formatDate(s.planStart)} – ${formatDate(s.planEnd)}`
              : "No active plan"
          }
          detail={
            s.frequencyPerWeek
              ? `${s.frequencyPerWeek}x/week for ${s.durationWeeks ?? "—"} weeks`
              : undefined
          }
        />
      </dl>
      <details className="mt-2">
        <summary className="cursor-pointer text-primary">
          Changes over the episode ({s.signedNotesUsed} signed note
          {s.signedNotesUsed === 1 ? "" : "s"})
        </summary>
        <div className="mt-2 space-y-2">
          {lists
            .filter(([, items]) => items.length > 0)
            .map(([title, items]) => (
              <div key={title}>
                <p className="font-bold text-[#333]">{title}</p>
                <ul className="list-disc pl-5 text-[#333]">
                  {items.map((i) => (
                    <li key={i}>{i}</li>
                  ))}
                </ul>
              </div>
            ))}
          {lists.every(([, items]) => items.length === 0) && (
            <p className="text-text-muted">
              No signed charting in this episode yet.
            </p>
          )}
        </div>
      </details>
    </>
  );
}

function Fact({
  label,
  value,
  detail,
}: {
  label: string;
  value: string;
  detail?: string;
}) {
  return (
    <div className="min-w-0">
      <dt className="text-text-muted">{label}</dt>
      <dd className="text-[#333]">
        {value}
        {detail && <span className="block text-text-muted">{detail}</span>}
      </dd>
    </div>
  );
}

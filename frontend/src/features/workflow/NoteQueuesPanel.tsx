import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { fetchNoteQueues } from "../charting/api";
import type { NoteQueueItem } from "../charting/types";
import { NoteStatusLabels, NoteTypeLabels } from "./types";

const formatDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
};

/** Documentation that still needs the user, beyond today's list: their
 * unsigned notes from earlier days (and open amendments), and other
 * clinicians' notes awaiting their cosignature. Hidden when there is none. */
export function NoteQueuesPanel({ today }: { today: string }) {
  const queues = useQuery({
    queryKey: ["workflow", "note-queues"],
    queryFn: fetchNoteQueues,
  });
  if (!queues.data) return null;
  // Today's drafts are already rows in the table below.
  const earlier = queues.data.myUnsignedNotes.filter(
    (n) => n.serviceDate < today || n.isAmendment,
  );
  const cosign = queues.data.awaitingMyCosign;
  if (earlier.length === 0 && cosign.length === 0) return null;

  return (
    <div className="mb-4 grid gap-3 lg:grid-cols-2">
      {earlier.length > 0 && (
        <Queue
          title="Your unfinished notes"
          hint="Drafts from earlier visits and notes waiting for a cosign."
          items={earlier}
          tone="warning"
        />
      )}
      {cosign.length > 0 && (
        <Queue
          title="Waiting for your cosign"
          hint="Assistants’ notes you supervise."
          items={cosign}
          tone="primary"
          showAuthor
        />
      )}
    </div>
  );
}

function Queue({
  title,
  hint,
  items,
  tone,
  showAuthor = false,
}: {
  title: string;
  hint: string;
  items: NoteQueueItem[];
  tone: "warning" | "primary";
  showAuthor?: boolean;
}) {
  return (
    <section
      aria-label={title}
      className={`rounded-lg border bg-white p-3 ${tone === "warning" ? "border-warning" : "border-primary"}`}
    >
      <h2 className="font-bold text-[#333]">
        {title} ({items.length})
      </h2>
      <p className="mb-2 text-text-muted">{hint}</p>
      <ul className="divide-y divide-border">
        {items.slice(0, 8).map((n) => (
          <li
            key={n.noteId}
            className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2"
          >
            <span className="text-[#333]">{formatDate(n.serviceDate)}</span>
            <span className="font-bold text-[#333]">{n.patientName}</span>
            <span className="text-text-muted">
              {NoteTypeLabels[n.noteType] ?? "Note"}
              {n.isAmendment ? " (amendment)" : ""} ·{" "}
              {NoteStatusLabels[n.status]}
              {showAuthor ? ` · ${n.authorName}` : ""}
            </span>
            <Link to={`/chart/${n.noteId}`} className="btn-refresh ml-auto">
              Open
            </Link>
          </li>
        ))}
      </ul>
      {items.length > 8 && (
        <p className="pt-2 text-text-muted">
          and {items.length - 8} more — oldest shown first.
        </p>
      )}
    </section>
  );
}

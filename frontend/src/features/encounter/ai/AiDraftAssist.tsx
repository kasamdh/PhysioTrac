import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  draftWithAi,
  fetchAiStatus,
  recordAiInserted,
  type AiDraft,
  type AiSection,
} from "./api";

const LABELS: Record<AiSection, string> = {
  assessment: "assessment",
  plan: "plan",
};

/** "Draft with AI" for the assessment or plan. The suggestion is shown
 * apart from the note; only the clinician's Insert puts it in the field,
 * where it is edited and saved like typed text. AI never signs. */
export function AiDraftAssist({
  noteId,
  section,
  currentText,
  onInsert,
}: {
  noteId: string;
  section: AiSection;
  currentText: string;
  onInsert: (text: string) => void;
}) {
  const status = useQuery({
    queryKey: ["documentation", "ai", "status"],
    queryFn: fetchAiStatus,
    staleTime: Infinity,
  });
  const [draft, setDraft] = useState<AiDraft | null>(null);
  const [inserted, setInserted] = useState(false);
  const request = useMutation({
    mutationFn: () => draftWithAi(noteId, section),
    onSuccess: (d) => {
      setDraft(d);
      setInserted(false);
    },
  });

  if (!status.data?.enabled) return null;

  const insert = (mode: "replace" | "append") => {
    if (!draft) return;
    onInsert(
      mode === "append" && currentText.trim()
        ? `${currentText.trimEnd()}\n${draft.text}`
        : draft.text,
    );
    // Best effort: the audit flag must not block the clinician's edit.
    void recordAiInserted(noteId, section).catch(() => {});
    setDraft(null);
    setInserted(true);
  };

  const panelId = `ai-${section}-draft`;
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          className="btn-refresh"
          disabled={request.isPending}
          aria-controls={draft ? panelId : undefined}
          onClick={() => request.mutate()}
        >
          {request.isPending
            ? "Drafting…"
            : draft
              ? "Draft again"
              : `Draft ${LABELS[section]} with AI`}
        </button>
        {inserted && (
          <span role="status" className="text-text-muted">
            AI draft inserted — review and edit it before signing.
          </span>
        )}
      </div>
      {request.isError && (
        <p role="alert" className="alert-error">
          {request.error.message}
        </p>
      )}
      {draft && (
        <div
          id={panelId}
          role="region"
          aria-label={`AI suggestion for the ${LABELS[section]}`}
          className="space-y-2 rounded-md border border-warning bg-warning-light p-3 text-[#333]"
        >
          <p className="font-bold">
            AI suggestion ({draft.provider}) — not part of the note until you
            insert it
          </p>
          <p className="whitespace-pre-wrap rounded border border-border bg-white p-2">
            {draft.text}
          </p>
          <p className="text-text-muted">{draft.notice}</p>
          <div className="flex flex-wrap gap-2">
            {currentText.trim() ? (
              <>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => insert("append")}
                >
                  Add below my text
                </button>
                <button
                  type="button"
                  className="btn-refresh"
                  onClick={() => insert("replace")}
                >
                  Replace my text
                </button>
              </>
            ) : (
              <button
                type="button"
                className="btn-primary"
                onClick={() => insert("replace")}
              >
                Insert into note
              </button>
            )}
            <button
              type="button"
              className="btn-refresh"
              onClick={() => setDraft(null)}
            >
              Discard
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { UserRoleLabels } from "../../auth/types";
import { searchPatients } from "../../schedule/api";
import { AdminPageHeader } from "../AdminPageHeader";
import { fetchMessageThreads, fetchThread, markThreadRead, sendMessage } from "../api";

function formatWhen(iso: string): string {
  const d = new Date(iso);
  const sameDay = d.toDateString() === new Date().toDateString();
  return sameDay
    ? d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })
    : `${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}`;
}

/** Secure-messaging inbox: threads (one per patient) on the left, the open
 * conversation on the right. The open thread is in the URL (?patient=). */
export function MessagesPage() {
  const [params, setParams] = useSearchParams();
  const patientId = params.get("patient");
  const [newPatientName, setNewPatientName] = useState<string | null>(null);
  const [composingNew, setComposingNew] = useState(false);

  const threads = useQuery({ queryKey: ["admin", "message-threads"], queryFn: fetchMessageThreads });
  const open = (id: string, name?: string) => {
    setComposingNew(false);
    setNewPatientName(name ?? null);
    setParams({ patient: id }, { replace: true });
  };

  const openThread = threads.data?.find((t) => t.patientId === patientId);
  const openName = openThread?.patientName ?? newPatientName;

  return (
    <div className="mx-auto max-w-6xl">
      <AdminPageHeader
        title="Messages"
        actions={
          <button type="button" className="btn-primary" onClick={() => setComposingNew(true)}>
            + New message
          </button>
        }
      />

      <div className="grid min-h-[60vh] grid-cols-1 overflow-hidden rounded-xl border border-border bg-surface shadow-sm md:grid-cols-[320px_1fr]">
        <aside className={`border-border md:border-r ${patientId || composingNew ? "hidden md:block" : ""}`}>
          {threads.isLoading && <p className="p-4 text-text-muted">Loading…</p>}
          {threads.isError && <p className="alert-error m-4">{threads.error.message}</p>}
          {threads.data && threads.data.length === 0 && (
            <p className="p-4 text-sm text-text-muted">No conversations yet. Start one with “New message”.</p>
          )}
          <ul>
            {threads.data?.map((t) => (
              <li key={t.patientId}>
                <button
                  type="button"
                  onClick={() => open(t.patientId)}
                  className={`block w-full border-b border-border px-4 py-3 text-left transition hover:bg-surface-muted ${
                    t.patientId === patientId ? "border-l-4 border-l-brand-accent bg-primary-light/50" : ""
                  }`}
                >
                  <div className="flex items-baseline justify-between gap-2">
                    <span className={`truncate text-text ${t.unreadCount > 0 ? "font-bold" : "font-medium"}`}>
                      {t.patientName}
                    </span>
                    <span className="shrink-0 text-xs text-text-subtle">{formatWhen(t.lastMessageAt)}</span>
                  </div>
                  <div className="mt-0.5 flex items-center justify-between gap-2">
                    <span className="truncate text-sm text-text-muted">{t.lastMessagePreview}</span>
                    {t.unreadCount > 0 && (
                      <span className="shrink-0 rounded-full bg-brand-accent px-1.5 text-xs font-semibold text-white">
                        {t.unreadCount}
                      </span>
                    )}
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </aside>

        <section className={`flex min-h-0 flex-col ${patientId || composingNew ? "" : "hidden md:flex"}`}>
          {composingNew ? (
            <NewConversation onCancel={() => setComposingNew(false)} onPick={(id, name) => open(id, name)} />
          ) : patientId ? (
            <Conversation
              key={patientId}
              patientId={patientId}
              patientName={openName}
              onBack={() => setParams({}, { replace: true })}
            />
          ) : (
            <p className="m-auto p-6 text-text-muted">Select a conversation.</p>
          )}
        </section>
      </div>
    </div>
  );
}

function Conversation({
  patientId,
  patientName,
  onBack,
}: {
  patientId: string;
  patientName: string | null;
  onBack: () => void;
}) {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const [draft, setDraft] = useState("");
  const endRef = useRef<HTMLDivElement>(null);

  const thread = useQuery({ queryKey: ["admin", "thread", patientId], queryFn: () => fetchThread(patientId) });

  const hasUnread = thread.data?.some((m) => !m.readAt && m.senderId !== user?.id) ?? false;
  useEffect(() => {
    if (!hasUnread) return;
    void markThreadRead(patientId).then(() =>
      queryClient.invalidateQueries({ queryKey: ["admin", "message-threads"] }),
    );
  }, [hasUnread, patientId, queryClient]);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end" });
  }, [thread.data?.length]);

  const send = useMutation({
    mutationFn: () => sendMessage(patientId, draft.trim()),
    onSuccess: () => {
      setDraft("");
      void queryClient.invalidateQueries({ queryKey: ["admin", "thread", patientId] });
      void queryClient.invalidateQueries({ queryKey: ["admin", "message-threads"] });
    },
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (draft.trim()) send.mutate();
  };

  return (
    <>
      <header className="flex items-center gap-3 border-b border-border px-4 py-3">
        <button type="button" className="btn-secondary md:hidden" onClick={onBack}>
          ‹ Back
        </button>
        <h2 className="font-semibold text-text">{patientName ?? "Conversation"}</h2>
      </header>
      <div className="min-h-[300px] flex-1 space-y-3 overflow-y-auto bg-surface-muted p-4">
        {thread.isLoading && <p className="text-text-muted">Loading…</p>}
        {thread.isError && <p className="alert-error">{thread.error.message}</p>}
        {thread.data?.length === 0 && <p className="text-sm text-text-muted">No messages yet — write the first one below.</p>}
        {thread.data?.map((m) => {
          const mine = m.senderId === user?.id;
          return (
            <div key={m.id} className={`flex ${mine ? "justify-end" : "justify-start"}`}>
              <div
                className={`max-w-[75%] rounded-lg px-3 py-2 text-sm shadow-sm ${
                  mine ? "bg-primary text-white" : "border border-border bg-white text-text"
                }`}
              >
                <p className="whitespace-pre-wrap">{m.body}</p>
                <p className={`mt-1 text-[11px] ${mine ? "text-white/75" : "text-text-subtle"}`}>
                  {m.isFromPatient ? "Patient" : mine ? "You" : UserRoleLabels[m.senderRole]} · {formatWhen(m.sentAt)}
                </p>
              </div>
            </div>
          );
        })}
        <div ref={endRef} />
      </div>
      <form onSubmit={onSubmit} className="flex gap-2 border-t border-border p-3">
        <textarea
          className="field-input min-h-[44px] flex-1 resize-y"
          rows={2}
          placeholder="Write a message…"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) onSubmit(e);
          }}
        />
        <button type="submit" className="btn-primary self-end" disabled={!draft.trim() || send.isPending}>
          {send.isPending ? "Sending…" : "Send"}
        </button>
      </form>
      {send.isError && <p className="alert-error mx-3 mb-3">{send.error.message}</p>}
    </>
  );
}

function NewConversation({ onCancel, onPick }: { onCancel: () => void; onPick: (id: string, name: string) => void }) {
  const [text, setText] = useState("");
  const [term, setTerm] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(() => setTerm(text.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [text]);

  const results = useQuery({
    queryKey: ["admin", "message-patient-search", term],
    queryFn: ({ signal }) => searchPatients(term, signal),
    enabled: term.length >= 2,
  });

  return (
    <div className="p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="font-semibold text-text">New message</h2>
        <button type="button" className="btn-secondary" onClick={onCancel}>
          Cancel
        </button>
      </div>
      <label className="field-label">
        Patient
        <input
          type="search"
          autoFocus
          className="field-input mt-1"
          placeholder="Search by name, MRN, phone or date of birth"
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
      </label>
      {term.length >= 2 && (
        <ul className="mt-2 divide-y divide-border rounded-md border border-border">
          {results.isLoading && <li className="px-3 py-2 text-sm text-text-muted">Searching…</li>}
          {results.data?.length === 0 && <li className="px-3 py-2 text-sm text-text-muted">No matching patients.</li>}
          {results.data?.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                className="block w-full px-3 py-2 text-left text-sm hover:bg-surface-muted"
                onClick={() => onPick(p.id, p.fullName)}
              >
                <span className="font-medium text-text">{p.fullName}</span>
                <span className="ml-2 text-text-muted">{p.medicalRecordNumber}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

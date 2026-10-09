import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { NoteStatusLabels } from "../../workflow/types";
import { fetchNoteVersions } from "../api";
import type { NoteVersion } from "../types";

// ClinicalNoteVersion.ContentJson (System.Text.Json, PascalCase).
interface VersionContent {
  Subjective?: string | null;
  Objective?: string | null;
  Interventions?: string | null;
  Assessment?: string | null;
  Plan?: string | null;
  Status?: number;
}

const parse = (json: string): VersionContent => {
  try {
    return JSON.parse(json) as VersionContent;
  } catch {
    return {};
  }
};

/** Every saved version of the note -- each autosave and the signed
 * version -- newest first; open one to read what it said then. Loaded only
 * when expanded. */
export function VersionHistory({ noteId }: { noteId: string }) {
  const [open, setOpen] = useState(false);
  const versions = useQuery({
    queryKey: ["chart", "versions", noteId],
    queryFn: () => fetchNoteVersions(noteId),
    enabled: open,
  });
  return (
    <div>
      <button
        type="button"
        className="table-link min-h-11 font-bold"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        {open ? "▾" : "▸"} Version history
      </button>
      {open && versions.isLoading && (
        <p className="text-text-muted">Loading…</p>
      )}
      {open && versions.isError && (
        <p className="alert-error">{versions.error.message}</p>
      )}
      {open && versions.data && (
        <ol className="mt-2 space-y-2">
          {[...versions.data].reverse().map((v) => (
            <VersionRow key={v.id} version={v} />
          ))}
        </ol>
      )}
    </div>
  );
}

function VersionRow({ version: v }: { version: NoteVersion }) {
  const [open, setOpen] = useState(false);
  const c = parse(v.contentJson);
  return (
    <li className="rounded-md border border-border">
      <button
        type="button"
        className="flex min-h-11 w-full flex-wrap items-center gap-x-3 px-3 py-2 text-left"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="font-bold text-primary">
          {open ? "▾" : "▸"} Version {v.versionNumber}
        </span>
        <span className={v.isSignedVersion ? "text-success" : "text-[#333]"}>
          {v.isSignedVersion
            ? "Signed version"
            : (NoteStatusLabels[c.Status ?? 0] ?? "Draft")}
        </span>
        <span className="text-text-muted">
          {new Date(v.createdAt).toLocaleString("en-US")}
          {v.savedByName ? ` · ${v.savedByName}` : ""}
        </span>
      </button>
      {open && (
        <dl className="space-y-2 border-t border-border px-3 py-2">
          {(
            [
              ["Subjective", c.Subjective],
              ["Objective", c.Objective],
              ["Treatment summary", c.Interventions],
              ["Assessment", c.Assessment],
              ["Plan", c.Plan],
            ] as const
          ).map(([label, value]) => (
            <div key={label}>
              <dt className="font-bold text-[#333]">{label}</dt>
              <dd className="whitespace-pre-wrap text-[#333]">
                {value || "—"}
              </dd>
            </div>
          ))}
        </dl>
      )}
    </li>
  );
}

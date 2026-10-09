import { useEffect, useRef, useState } from "react";
import type { OutputKind } from "./api";

/** "10/07/2026, 3:00 PM CDT" in the clinic's time zone, whatever the viewer's. */
export function formatInZone(iso: string, timeZone: string | undefined) {
  try {
    return new Date(iso).toLocaleString("en-US", {
      timeZone,
      // (dateStyle/timeStyle can't be combined with timeZoneName.)
      month: "2-digit",
      day: "2-digit",
      year: "numeric",
      hour: "numeric",
      minute: "2-digit",
      timeZoneName: "short",
    });
  } catch {
    // Unknown zone: fall back to the viewer's.
    return new Date(iso).toLocaleString("en-US");
  }
}

/** Prints only after the print or export has been recorded in the audit
 * trail; a refusal (no permission to view) is shown instead of printing.
 * Once `ready`, the browser's print dialog opens once by itself. */
export function usePrintOutput(
  ready: boolean,
  record: (kind: OutputKind) => Promise<unknown>,
) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const recordRef = useRef(record);
  recordRef.current = record;

  const output = async (kind: OutputKind) => {
    setBusy(true);
    setError(null);
    try {
      await recordRef.current(kind);
      window.print();
    } catch (e) {
      setError(e instanceof Error ? e.message : "This can't be printed.");
    } finally {
      setBusy(false);
    }
  };

  const started = useRef(false);
  useEffect(() => {
    if (!ready || started.current) return;
    started.current = true;
    // Give late tables (interventions) a moment to render before printing.
    const t = window.setTimeout(() => void output("print"), 800);
    return () => window.clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready]);

  return { output, error, busy };
}

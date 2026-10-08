import type { usePrintOutput } from "./usePrintOutput";

/** Print / Save as PDF / Close, hidden on paper. */
export function PrintActions({
  output,
  error,
  busy,
}: ReturnType<typeof usePrintOutput>) {
  return (
    <div className="mb-4 space-y-2 print:hidden">
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          className="btn-primary"
          disabled={busy}
          onClick={() => void output("print")}
        >
          Print
        </button>
        <button
          type="button"
          className="btn-refresh"
          disabled={busy}
          onClick={() => void output("export")}
        >
          Save as PDF
        </button>
        <button
          type="button"
          className="btn-refresh"
          onClick={() => window.close()}
        >
          Close
        </button>
      </div>
      <p className="text-text-muted">
        For a PDF, choose “Save as PDF” as the printer. Every print and export
        is recorded in the audit log.
      </p>
      {error && (
        <p role="alert" className="alert-error">
          {error}
        </p>
      )}
    </div>
  );
}

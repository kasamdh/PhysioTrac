import { Fragment, useEffect, useId, useRef } from "react";
import { createPortal } from "react-dom";
import { headerSection, pageHelp, type HelpSection } from "./helpContent";

/** Page instructions in a modal: navy title bar with print, links to each
 * section, the page's own sections, then the shared Header section.
 * Rendered outside the app root so printing it prints only the help. */
export function HelpDialog({ helpKey, onClose }: { helpKey: string; onClose: () => void }) {
  const titleId = useId();
  const topRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const help = pageHelp[helpKey] ?? pageHelp.home;
  const sections: HelpSection[] = [...help.sections, headerSection];

  useEffect(() => {
    closeRef.current?.focus();
    document.body.dataset.helpOpen = "true";
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => {
      delete document.body.dataset.helpOpen;
      document.removeEventListener("keydown", onKey);
    };
  }, [onClose]);

  const jump = (id: string) =>
    document.getElementById(`help-${id}`)?.scrollIntoView({ behavior: "smooth", block: "start" });

  return createPortal(
    <div className="modal-overlay help-overlay" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
        className="help-dialog flex max-h-[92vh] w-full max-w-4xl flex-col overflow-hidden rounded-lg bg-white p-3 shadow-2xl sm:p-4"
      >
        <div className="flex items-center gap-2 bg-[#0c3d66] px-3 py-2 text-white">
          <h2 id={titleId} className="flex-1 text-center text-2xl sm:text-3xl">
            {help.title} Help
          </h2>
          <button
            type="button"
            onClick={() => window.print()}
            aria-label="Print these instructions"
            title="Print"
            className="flex h-11 w-11 items-center justify-center rounded hover:bg-white/10 print:hidden"
          >
            <svg viewBox="0 0 24 24" className="h-6 w-6" fill="currentColor" aria-hidden="true">
              <path d="M7 3h10v4H7zM5 8h14a2 2 0 0 1 2 2v6h-4v4H7v-4H3v-6a2 2 0 0 1 2-2zm4 7v3h6v-3zm8-4.5a1 1 0 1 0 0 2 1 1 0 0 0 0-2z" />
            </svg>
          </button>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            aria-label="Close help"
            className="flex h-11 w-11 items-center justify-center rounded text-3xl leading-none hover:bg-white/10 print:hidden"
          >
            ×
          </button>
        </div>

        <div ref={topRef} className="help-body overflow-y-auto px-1 pt-4 pb-2 text-[#333] sm:px-2">
          <nav aria-label="Help sections" className="text-center text-[#2a6eb0] print:hidden">
            {sections.map((s, i) => (
              <Fragment key={s.id}>
                {i > 0 && <span className="mx-2 text-[#333]">|</span>}
                <button type="button" className="hover:underline" onClick={() => jump(s.id)}>
                  {s.title}
                </button>
              </Fragment>
            ))}
          </nav>

          <div className="mt-4 space-y-2">{help.intro}</div>

          {sections.map((s) => (
            <section key={s.id} id={`help-${s.id}`} aria-labelledby={`help-${s.id}-title`} className="mt-6 scroll-mt-2">
              <h3 id={`help-${s.id}-title`} className="mb-3 text-2xl font-bold text-[#29a8e0]">
                {s.title}
              </h3>
              {s.body}
              <button
                type="button"
                className="mt-1 text-[#2a6eb0] hover:underline print:hidden"
                onClick={() => topRef.current?.scrollTo({ top: 0, behavior: "smooth" })}
              >
                Back to Top
              </button>
            </section>
          ))}
        </div>
      </div>
    </div>,
    document.body,
  );
}

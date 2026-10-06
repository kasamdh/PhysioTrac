import { kindColors } from "../status";

/** Key for the visit-type colors on appointment cards. */
export function VisitTypeLegend() {
  return (
    <ul aria-label="Visit type colors" className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-[#333]">
      {Object.values(kindColors).map((k) => (
        <li key={k.label} className="flex items-center gap-1.5">
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 rounded-sm border-l-4"
            style={{ background: k.bg, borderLeftColor: k.accent }}
          />
          {k.label}
        </li>
      ))}
      <li className="flex items-center gap-1.5 text-text-muted">
        <span aria-hidden="true" className="h-3.5 w-3.5 rounded-sm border-l-4 border-danger bg-surface-muted" />
        <span className="line-through">Cancelled / no-show</span>
      </li>
    </ul>
  );
}

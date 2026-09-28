import type { ResourceHeaderProps } from "react-big-calendar";
import { formatHhmm } from "../time";
import type { ProviderDayColumn } from "../types";

export interface ProviderResource {
  id: string;
  title: string;
  column: ProviderDayColumn | null; // null for the "Unassigned" column
}

/** Day-view column header: name, credentials, location and hours, and how
 * many appointments are on the books today. */
export function ProviderColumnHeader({ resource }: ResourceHeaderProps<ProviderResource>) {
  const column = resource.column;
  if (!column) {
    return <div className="py-1 text-sm font-semibold text-text-muted">{resource.title}</div>;
  }
  const { provider, workingHours, summary } = column;
  const booked = summary.appointments - summary.cancelled;
  return (
    <div className="py-1 text-left leading-tight">
      <div className="truncate text-sm font-semibold text-text" title={provider.name}>
        {provider.name}
        {provider.credentials && <span className="font-normal text-text-muted">, {provider.credentials}</span>}
      </div>
      <div className="truncate text-xs text-text-muted">
        {workingHours.length === 0
          ? "No hours set"
          : workingHours.map((w) => `${formatHhmm(w.start)}–${formatHhmm(w.end)}${w.locationName ? ` · ${w.locationName}` : ""}`).join(", ")}
      </div>
      <div className="text-xs font-medium text-primary-deep">
        {booked} appointment{booked === 1 ? "" : "s"}
      </div>
    </div>
  );
}

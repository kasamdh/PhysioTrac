import type { ResourceHeaderProps } from "react-big-calendar";
import { formatHhmm } from "../time";
import type { ProviderDayColumn } from "../types";

export interface ProviderResource {
  id: string;
  title: string;
  column: ProviderDayColumn | null; // null for the "Unassigned" column
}

/** Day-view column header: name, credentials, location and hours, and how
 * many appointments are on the books today. Clicking it shows only that
 * provider's schedule (the choice then carries across Day/Week/Month/Year/
 * List); clicking it again, while it's the only column, shows everyone. */
export function makeProviderColumnHeader(selectedProviderId: string, onSelect: (providerId: string) => void) {
  return function ProviderColumnHeader({ resource }: ResourceHeaderProps<ProviderResource>) {
    const column = resource.column;
    if (!column) {
      return <div className="py-1 text-sm font-semibold text-text-muted">{resource.title}</div>;
    }
    const { provider, workingHours, summary } = column;
    const booked = summary.appointments - summary.cancelled;
    const isSelected = selectedProviderId === provider.id;
    return (
      <button
        type="button"
        onClick={() => onSelect(isSelected ? "" : provider.id)}
        aria-pressed={isSelected}
        title={isSelected ? "Show all providers" : `Show only ${provider.name}'s schedule`}
        className="-mx-1 block w-[calc(100%+0.5rem)] rounded-md px-1 py-1 text-left leading-tight transition hover:bg-primary-light/60 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        <span className="block truncate text-sm font-semibold text-text">
          {provider.name}
          {provider.credentials && <span className="font-normal text-text-muted">, {provider.credentials}</span>}
        </span>
        <span className="block truncate text-xs text-text-muted">
          {workingHours.length === 0
            ? "No hours set"
            : workingHours.map((w) => `${formatHhmm(w.start)}–${formatHhmm(w.end)}${w.locationName ? ` · ${w.locationName}` : ""}`).join(", ")}
        </span>
        <span className="block text-xs font-medium text-primary-deep">
          {booked} appointment{booked === 1 ? "" : "s"}
          <span className="ml-1 font-normal text-text-subtle">· {isSelected ? "show all" : "view schedule"}</span>
        </span>
      </button>
    );
  };
}

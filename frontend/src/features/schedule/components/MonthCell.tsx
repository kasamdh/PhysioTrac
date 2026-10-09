import type { DateHeaderProps, EventProps } from "react-big-calendar";
import { formatTime, toDateKey } from "../time";
import type { CalendarBlock, CalendarEvent } from "./AppointmentCard";

/** Month view entry: one line, "9:00 AM John Smith" -- the full card
 * belongs to Day/Week; a month cell only has room for a glance. */
export function MonthEvent({ event }: EventProps<CalendarEvent | CalendarBlock>) {
  if (!("appointment" in event)) return <span>{event.title}</span>;
  return (
    <span className="block truncate text-sm leading-tight">
      <span className="opacity-80">{formatTime(event.start)}</span> <span className="font-semibold">{event.appointment.patientName}</span>
    </span>
  );
}

/** Month day header: the date plus how many (non-cancelled) appointments
 * that day has. Clicking it opens the Day view for that date. */
export function makeMonthDateHeader(countsByDay: Map<string, number>) {
  return function MonthDateHeader({ date, label, onDrillDown }: DateHeaderProps) {
    const count = countsByDay.get(toDateKey(date)) ?? 0;
    return (
      <button type="button" onClick={onDrillDown} className="flex w-full items-center justify-between gap-1 text-left hover:text-primary">
        <span className="text-sm font-medium text-primary-deep">{count > 0 ? `${count} appt${count === 1 ? "" : "s"}` : ""}</span>
        <span>{label}</span>
      </button>
    );
  };
}

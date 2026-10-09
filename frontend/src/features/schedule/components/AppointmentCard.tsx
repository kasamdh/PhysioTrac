import type { EventProps } from "react-big-calendar";
import { noteTag } from "../status";
import { formatTime } from "../time";
import { AppointmentKind, AppointmentStatusLabels, type ScheduleAppointment } from "../types";

export interface CalendarEvent {
  id: string;
  title: string;
  start: Date;
  end: Date;
  resourceId: string;
  appointment: ScheduleAppointment;
}

/** Background (non-appointment) blocks: lunch, time off, closures. */
export interface CalendarBlock {
  id: string;
  title: string;
  start: Date;
  end: Date;
  resourceId: string;
}

function Tags({ a }: { a: ScheduleAppointment }) {
  const note = noteTag(a);
  return (
    <>
      {a.kind === AppointmentKind.Evaluation && <span className="appt-tag">New</span>}
      {a.kind === AppointmentKind.Telehealth && <span className="appt-tag">Telehealth</span>}
      {a.isHomeVisit && <span className="appt-tag">Home</span>}
      {note && <span className="appt-tag">{note}</span>}
    </>
  );
}

/** Calendar card: time, patient, type, status -- plus the provider when the
 * view mixes several providers' appointments in one column (Week/Month
 * with "All providers"). Visits of 30 minutes or less get a two-line
 * layout so nothing is squeezed into a short card. */
export function makeAppointmentCard(showProvider: boolean) {
  return function AppointmentCard({ event }: EventProps<CalendarEvent | CalendarBlock>) {
    if (!("appointment" in event)) return <span>{event.title}</span>;
    const a = event.appointment;
    const line = "block shrink-0 truncate";

    if (a.durationMinutes <= 30) {
      return (
        <div className="flex h-full flex-col overflow-hidden leading-tight">
          <span className={`${line} text-sm`}>
            {formatTime(event.start)} · {AppointmentStatusLabels[a.status]}
            {showProvider && a.providerName ? ` · ${a.providerName}` : ""}
          </span>
          <span className={`${line} text-xs font-semibold`}>
            {a.patientName}
            <Tags a={a} />
          </span>
        </div>
      );
    }

    return (
      <div className="flex h-full flex-col gap-0.5 overflow-hidden leading-tight">
        <span className={`${line} text-sm opacity-80`}>
          {formatTime(event.start)} – {formatTime(event.end)}
        </span>
        <span className={`${line} text-xs font-semibold`}>{a.patientName}</span>
        <span className={`${line} text-sm`}>
          {a.appointmentTypeName ?? "Appointment"}
          <Tags a={a} />
        </span>
        {showProvider && a.providerName && <span className={`${line} text-sm opacity-80`}>{a.providerName}</span>}
        <span className={`${line} text-sm font-medium`}>{AppointmentStatusLabels[a.status]}</span>
      </div>
    );
  };
}

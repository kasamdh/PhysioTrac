/*
 * Clinic-timezone helpers. Appointments are stored as absolute instants,
 * but the calendar must show them on the *clinic's* wall clock (a front
 * desk in another timezone, or a laptop set to UTC, must still see a 9:00
 * visit at 9:00). The calendar library only understands the browser's own
 * timezone, so the calendar is fed "wall-clock" Dates: a Date whose
 * browser-local fields equal the clinic-local time. Convert back with
 * wallClockToIso before sending anything to the Api.
 */

const partsFormatters = new Map<string, Intl.DateTimeFormat>();

function partsFormatter(timeZone: string): Intl.DateTimeFormat {
  let formatter = partsFormatters.get(timeZone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat("en-US", {
      timeZone,
      hourCycle: "h23",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
    partsFormatters.set(timeZone, formatter);
  }
  return formatter;
}

function zonedFields(instant: Date, timeZone: string) {
  const parts = Object.fromEntries(partsFormatter(timeZone).formatToParts(instant).map((p) => [p.type, p.value]));
  return {
    year: Number(parts.year),
    month: Number(parts.month),
    day: Number(parts.day),
    hour: Number(parts.hour),
    minute: Number(parts.minute),
    second: Number(parts.second),
  };
}

/** An Api instant (ISO string) as a clinic wall-clock Date for the calendar. */
export function toWallClock(iso: string, timeZone: string): Date {
  const f = zonedFields(new Date(iso), timeZone);
  return new Date(f.year, f.month - 1, f.day, f.hour, f.minute, f.second);
}

/** Offset of `timeZone` from UTC, in minutes, at the given instant. */
function offsetMinutes(instant: Date, timeZone: string): number {
  const f = zonedFields(instant, timeZone);
  const asUtc = Date.UTC(f.year, f.month - 1, f.day, f.hour, f.minute, f.second);
  return Math.round((asUtc - instant.getTime()) / 60000);
}

const pad = (n: number) => String(Math.abs(n)).padStart(2, "0");

/** A clinic wall-clock Date back to an ISO string with the clinic's real
 * UTC offset for that moment (DST-correct), for sending to the Api. */
export function wallClockToIso(wall: Date, timeZone: string): string {
  const naiveUtc = Date.UTC(wall.getFullYear(), wall.getMonth(), wall.getDate(), wall.getHours(), wall.getMinutes(), wall.getSeconds());
  // Two passes settle the offset across a DST boundary.
  let offset = offsetMinutes(new Date(naiveUtc), timeZone);
  offset = offsetMinutes(new Date(naiveUtc - offset * 60000), timeZone);
  const sign = offset >= 0 ? "+" : "-";
  return (
    `${wall.getFullYear()}-${pad(wall.getMonth() + 1)}-${pad(wall.getDate())}` +
    `T${pad(wall.getHours())}:${pad(wall.getMinutes())}:${pad(wall.getSeconds())}` +
    `${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`
  );
}

/** "YYYY-MM-DD" for a wall-clock Date's own (browser-local) fields. */
export function toDateKey(wall: Date): string {
  return `${wall.getFullYear()}-${pad(wall.getMonth() + 1)}-${pad(wall.getDate())}`;
}

/** Wall-clock midnight for a "YYYY-MM-DD" key. */
export function fromDateKey(key: string): Date {
  const [y, m, d] = key.split("-").map(Number);
  return new Date(y, m - 1, d);
}

/** Today's date in the clinic's timezone, as "YYYY-MM-DD". */
export function todayKey(timeZone: string): string {
  return toDateKey(toWallClock(new Date().toISOString(), timeZone));
}

/** Wall-clock Date for "HH:mm" on a given day. */
export function atTime(day: Date, hhmm: string): Date {
  const [h, m] = hhmm.split(":").map(Number);
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), h, m);
}

export function addDays(wall: Date, days: number): Date {
  return new Date(wall.getFullYear(), wall.getMonth(), wall.getDate() + days, wall.getHours(), wall.getMinutes());
}

/** Monday on or before the given day (the calendar's week starts Monday). */
export function startOfWeek(wall: Date): Date {
  return addDays(new Date(wall.getFullYear(), wall.getMonth(), wall.getDate()), -((wall.getDay() + 6) % 7));
}

export function formatTime(wall: Date): string {
  return wall.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });
}

export function formatHhmm(hhmm: string): string {
  return formatTime(atTime(new Date(2000, 0, 1), hhmm));
}

export function formatHours(minutes: number): string {
  const hours = minutes / 60;
  return `${Number.isInteger(hours) ? hours : hours.toFixed(1)}h`;
}

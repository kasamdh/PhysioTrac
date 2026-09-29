import { useCallback, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { Calendar, dayjsLocalizer, type SlotPropGetter, type View } from "react-big-calendar";
import withDragAndDropModule, { type EventInteractionArgs } from "react-big-calendar/lib/addons/dragAndDrop";
import dayjs from "dayjs";
import updateLocale from "dayjs/plugin/updateLocale";
import "react-big-calendar/lib/css/react-big-calendar.css";
import "react-big-calendar/lib/addons/dragAndDrop/styles.css";
import "./schedule.css";
import { fetchScheduleCounts, fetchScheduleDay, fetchScheduleList, fetchScheduleRange, fetchScheduleSettings } from "./api";
import { makeAppointmentCard, type CalendarBlock, type CalendarEvent } from "./components/AppointmentCard";
import { AppointmentDetailsPanel } from "./components/AppointmentDetailsPanel";
import { MoveAppointmentDialog } from "./components/MoveAppointmentDialog";
import { NewAppointmentDialog, type NewAppointmentDefaults } from "./components/NewAppointmentDialog";
import { RescheduleDialog } from "./components/RescheduleDialog";
import { MonthEvent, makeMonthDateHeader } from "./components/MonthCell";
import { makeProviderColumnHeader, type ProviderResource } from "./components/ProviderColumnHeader";
import { ProviderDailySummary } from "./components/ProviderDailySummary";
import { ScheduleToolbar } from "./components/ScheduleToolbar";
import { ListView } from "./components/ListView";
import { YearView } from "./components/YearView";
import { isReschedulable, statusEventClass } from "./status";
import { useToast } from "../../components/Toast";
import {
  addDays, atTime, formatTime, fromDateKey, startOfWeek, toDateKey, todayKey, toWallClock, wallClockToIso,
} from "./time";
import {
  AppointmentStatus, type ListRange, type ProviderDayColumn, type RescheduleRequest, type ScheduleAppointment, type ScheduleFilters, type ScheduleView,
} from "./types";

// Weeks start on Monday, like a clinic's working week.
dayjs.extend(updateLocale);
dayjs.updateLocale("en", { weekStart: 1 });
const localizer = dayjsLocalizer(dayjs);
// The drag-and-drop addon ships only as CommonJS; depending on the bundler's
// interop, the default import is either the function or the exports object
// holding it under `.default`.
const withDragAndDrop =
  (withDragAndDropModule as unknown as { default?: typeof withDragAndDropModule }).default ?? withDragAndDropModule;
const DnDCalendar = withDragAndDrop<CalendarEvent | CalendarBlock, ProviderResource>(Calendar);

const UNASSIGNED = "unassigned";
const LOCATION_STORAGE_KEY = "physiotrac.selectedLocationId"; // shared with the header's location picker

const formats = {
  timeGutterFormat: "h A",
  dateFormat: "D",
  dayFormat: "ddd M/D",
  weekdayFormat: "ddd",
};

function rememberedLocation(): string {
  try {
    return localStorage.getItem(LOCATION_STORAGE_KEY) ?? "";
  } catch {
    return "";
  }
}

/** The staff Schedule page. View, date, and filters live in the URL, so
 * switching views keeps them and a link reproduces exactly what's shown. */
export function SchedulePage() {
  const [params, setParams] = useSearchParams();
  const [selected, setSelected] = useState<ScheduleAppointment | null>(null);
  const closeDetails = useCallback(() => setSelected(null), []);
  const [pendingMove, setPendingMove] = useState<{ appointment: ScheduleAppointment; request: RescheduleRequest } | null>(null);
  const [rescheduling, setRescheduling] = useState<ScheduleAppointment | null>(null);
  const [newAppointment, setNewAppointment] = useState<NewAppointmentDefaults | null>(null);
  const { showToast } = useToast();

  const settingsQuery = useQuery({ queryKey: ["schedule", "settings"], queryFn: fetchScheduleSettings });
  const settings = settingsQuery.data;
  const tz = settings?.timezone ?? "UTC";

  const isNarrow = typeof window !== "undefined" && window.matchMedia("(max-width: 639px)").matches;
  // On a phone the List view is the default: a multi-column calendar doesn't fit.
  const view = (params.get("view") as ScheduleView | null) ?? (isNarrow ? "list" : "week");
  const listRange = (params.get("listRange") as ListRange | null) ?? "day";
  const page = Math.max(1, Number(params.get("page")) || 1);
  const dateKey = params.get("date") ?? todayKey(tz);
  const settingsLocations = settings?.locations;
  // "all" in the URL is an explicit "All locations"; no param at all means
  // "start from the location picked in the header".
  const filters: ScheduleFilters = useMemo(() => {
    const rememberedLocationId = rememberedLocation();
    const locationParam = params.get("location");
    return {
      locationId:
        locationParam === "all" ? ""
        : (locationParam ?? (settingsLocations?.some((l) => l.id === rememberedLocationId) ? rememberedLocationId : "")),
      providerId: params.get("provider") ?? "",
      status: params.get("status") ?? "",
      appointmentTypeId: params.get("type") ?? "",
      patient: params.get("patient") ?? "",
    };
  }, [params, settingsLocations]);

  const update = useCallback(
    (changes: Record<string, string>) => {
      setParams(
        () => {
          // Read the live URL, not the render-time `prev`: two quick updates
          // (pick a filter, then click Week) would otherwise both start from
          // the same stale params and the second would undo the first.
          const next = new URLSearchParams(window.location.search);
          for (const [key, value] of Object.entries(changes)) {
            if (value) next.set(key, value);
            else next.delete(key);
          }
          return next;
        },
        { replace: true },
      );
    },
    [setParams],
  );

  const onFiltersChange = useCallback(
    (changes: Partial<ScheduleFilters>) => {
      const map: Record<keyof ScheduleFilters, string> = {
        locationId: "location", providerId: "provider", status: "status", appointmentTypeId: "type", patient: "patient",
      };
      const next: Record<string, string> = {};
      for (const [key, value] of Object.entries(changes)) next[map[key as keyof ScheduleFilters]] = value ?? "";
      if (changes.locationId === "") {
        next.location = "all";
      } else if (changes.locationId) {
        try {
          localStorage.setItem(LOCATION_STORAGE_KEY, changes.locationId);
        } catch {
          // Best-effort only, same as the header's picker.
        }
      }
      // Any filter change starts the List view back on page 1.
      update({ ...next, page: "" });
    },
    [update],
  );

  const current = useMemo(() => fromDateKey(dateKey), [dateKey]);
  const rangeStart = view === "week" ? startOfWeek(current) : startOfWeek(new Date(current.getFullYear(), current.getMonth(), 1));
  const rangeEnd = view === "week" ? addDays(rangeStart, 7) : addDays(rangeStart, 42);
  const year = current.getFullYear();

  // List view covers the selected day, its week, or its month.
  const listStart =
    listRange === "day" ? current : listRange === "week" ? startOfWeek(current) : new Date(current.getFullYear(), current.getMonth(), 1);
  const listEnd =
    listRange === "day" ? addDays(current, 1)
    : listRange === "week" ? addDays(listStart, 7)
    : new Date(current.getFullYear(), current.getMonth() + 1, 1);

  const dayQuery = useQuery({
    queryKey: ["schedule", "day", dateKey, filters.locationId, filters.providerId],
    queryFn: () => fetchScheduleDay(dateKey, filters.locationId, filters.providerId),
    enabled: !!settings && view === "day",
  });
  const rangeQuery = useQuery({
    queryKey: ["schedule", "range", view, toDateKey(rangeStart), filters],
    queryFn: () => fetchScheduleRange(wallClockToIso(rangeStart, tz), wallClockToIso(rangeEnd, tz), filters),
    enabled: !!settings && (view === "week" || view === "month"),
  });
  const countsQuery = useQuery({
    queryKey: ["schedule", "counts", year, filters.locationId, filters.providerId],
    queryFn: () => fetchScheduleCounts(`${year}-01-01`, `${year}-12-31`, filters.locationId, filters.providerId),
    enabled: !!settings && view === "year",
  });
  const listQuery = useQuery({
    queryKey: ["schedule", "list", listRange, toDateKey(listStart), filters, page],
    queryFn: () => fetchScheduleList(wallClockToIso(listStart, tz), wallClockToIso(listEnd, tz), filters, page, 25),
    enabled: !!settings && view === "list",
    placeholderData: (previous) => previous,
  });
  const yearCounts = useMemo(
    () => new Map((countsQuery.data ?? []).map((c) => [c.date, c.count])),
    [countsQuery.data],
  );

  // Day view: the Api filters by location/provider; status/type/patient are
  // applied here, since one day's appointments are already loaded.
  const appointments = useMemo(() => {
    if (view !== "day") return rangeQuery.data?.appointments ?? [];
    const term = filters.patient.toLowerCase();
    return (dayQuery.data?.appointments ?? []).filter(
      (a) =>
        (!filters.status || String(a.status) === filters.status) &&
        (!filters.appointmentTypeId || a.appointmentTypeId === filters.appointmentTypeId) &&
        (!term || a.patientName.toLowerCase().includes(term) || a.medicalRecordNumber.toLowerCase().includes(term)),
    );
  }, [view, dayQuery.data, rangeQuery.data, filters.status, filters.appointmentTypeId, filters.patient]);

  const columns: ProviderDayColumn[] = useMemo(() => dayQuery.data?.providers ?? [], [dayQuery.data]);

  const events: CalendarEvent[] = useMemo(
    () =>
      appointments.map((a) => ({
        id: a.id,
        title: a.patientName,
        start: toWallClock(a.startsAt, tz),
        end: toWallClock(a.endsAt, tz),
        resourceId: a.providerId ?? UNASSIGNED,
        appointment: a,
      })),
    [appointments, tz],
  );

  const resources: ProviderResource[] | undefined = useMemo(() => {
    if (view !== "day") return undefined;
    const list: ProviderResource[] = columns.map((c) => ({ id: c.provider.id, title: c.provider.name, column: c }));
    if (events.some((e) => e.resourceId === UNASSIGNED)) list.push({ id: UNASSIGNED, title: "Unassigned", column: null });
    return list;
  }, [view, columns, events]);

  const blocks: CalendarBlock[] = useMemo(
    () =>
      view !== "day"
        ? []
        : columns.flatMap((c) =>
            c.blocks.map((b, i) => ({
              id: `${c.provider.id}-${i}`,
              title: b.kind === "Closure" ? `Closed: ${b.reason}` : b.reason,
              start: toWallClock(b.startsAt, tz),
              end: toWallClock(b.endsAt, tz),
              resourceId: c.provider.id,
            })),
          ),
    [view, columns, tz],
  );

  // Shade each provider's column outside their working hours.
  const slotPropGetter: SlotPropGetter = useCallback(
    (date, resourceId) => {
      if (view !== "day") return {};
      const column = columns.find((c) => c.provider.id === resourceId);
      if (!column || column.workingHours.length === 0) return {};
      const inHours = column.workingHours.some((w) => atTime(date, w.start) <= date && date < atTime(date, w.end));
      return inHours ? {} : { className: "slot-off-hours" };
    },
    [view, columns],
  );

  // Visible hours: 7 AM-7 PM by default, widened to fit working hours and
  // any appointment that falls outside them.
  const { min, max } = useMemo(() => {
    let startHour = 7;
    let endHour = 19;
    for (const c of columns) {
      for (const w of c.workingHours) {
        startHour = Math.min(startHour, Number(w.start.slice(0, 2)));
        endHour = Math.max(endHour, Math.ceil(Number(w.end.slice(0, 2)) + Number(w.end.slice(3)) / 60));
      }
    }
    for (const e of events) {
      startHour = Math.min(startHour, e.start.getHours());
      endHour = Math.max(endHour, e.end.getHours() + (e.end.getMinutes() > 0 ? 1 : 0));
    }
    return { min: atTime(current, `${startHour}:00`), max: atTime(current, `${Math.min(endHour, 23)}:59`) };
  }, [columns, events, current]);

  const countsByDay = useMemo(() => {
    const counts = new Map<string, number>();
    for (const e of events) {
      if (e.appointment.status === AppointmentStatus.Cancelled) continue;
      const key = toDateKey(e.start);
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    return counts;
  }, [events]);

  const components = useMemo(
    () => ({
      event: makeAppointmentCard(view !== "day" && !filters.providerId),
      resourceHeader: makeProviderColumnHeader(filters.providerId, (providerId) => onFiltersChange({ providerId })),
      month: { event: MonthEvent, dateHeader: makeMonthDateHeader(countsByDay) },
    }),
    [view, filters.providerId, countsByDay, onFiltersChange],
  );

  const eventPropGetter = useCallback(
    (event: CalendarEvent | CalendarBlock) =>
      "appointment" in event
        ? {
            className: statusEventClass[event.appointment.status],
            style: event.appointment.appointmentTypeColor ? { borderLeftColor: event.appointment.appointmentTypeColor } : undefined,
          }
        : { className: "schedule-block" },
    [],
  );

  const title =
    view === "day"
      ? current.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric", year: "numeric" })
      : view === "week"
        ? `${rangeStart.toLocaleDateString("en-US", { month: "short", day: "numeric" })} – ${addDays(rangeStart, 6).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}`
        : view === "year"
          ? String(year)
          : view === "list" && listRange === "day"
            ? current.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric", year: "numeric" })
            : view === "list" && listRange === "week"
              ? `${listStart.toLocaleDateString("en-US", { month: "short", day: "numeric" })} – ${addDays(listStart, 6).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}`
              : current.toLocaleDateString("en-US", { month: "long", year: "numeric" });

  function step(direction: -1 | 1) {
    const unit = view === "list" ? listRange : view;
    const next =
      unit === "day" ? addDays(current, direction)
      : unit === "week" ? addDays(current, 7 * direction)
      : unit === "year" ? new Date(current.getFullYear() + direction, current.getMonth(), 1)
      : new Date(current.getFullYear(), current.getMonth() + direction, 1);
    update({ date: toDateKey(next), page: "" });
  }

  if (settingsQuery.isLoading) return <p className="text-text-muted">Loading schedule…</p>;
  if (settingsQuery.isError || !settings) {
    return <p className="alert-error">Could not load the schedule: {settingsQuery.error?.message}</p>;
  }

  const activeQuery = view === "day" ? dayQuery : view === "year" ? countsQuery : view === "list" ? listQuery : rangeQuery;
  const isCalendar = view === "day" || view === "week" || view === "month";
  const selectedProvider = filters.providerId ? settings.providers.find((p) => p.id === filters.providerId) : undefined;

  // Drag-and-drop and resize: Day and Week only (a Month cell has no time
  // to drop onto), only for users the Api lets reschedule, and only for
  // visits that haven't started or finished.
  const canDrag = (e: CalendarEvent | CalendarBlock) =>
    view !== "month" && settings.canReschedule && "appointment" in e && isReschedulable(e.appointment.status);

  /** Clicking/dragging across empty time in Day or Week opens New
   * appointment with that time, length, provider (Day view column), and the
   * location that provider works at then. */
  function handleSelectSlot({ start, end, resourceId }: { start: Date; end: Date; resourceId?: string | number }) {
    if (!settings?.canCreate || view === "month") return;
    const providerId = resourceId !== undefined && String(resourceId) !== UNASSIGNED ? String(resourceId) : filters.providerId || undefined;
    const window_ = columns
      .find((c) => c.provider.id === providerId)
      ?.workingHours.find((w) => atTime(start, w.start) <= start && start < atTime(start, w.end));
    const minutes = Math.round((end.getTime() - start.getTime()) / 60000);
    setNewAppointment({
      date: toDateKey(start),
      time: `${String(start.getHours()).padStart(2, "0")}:${String(start.getMinutes()).padStart(2, "0")}`,
      durationMinutes: minutes > slotMinutes ? minutes : undefined,
      providerId,
      locationId: window_?.locationId ?? (filters.locationId || undefined),
    });
  }

  /** Nothing is saved here -- a drop only opens the Move dialog, which
   * dry-runs the move on the server and asks for confirmation. */
  function handleDrop({ event, start, end, resourceId }: EventInteractionArgs<CalendarEvent | CalendarBlock>) {
    if (!("appointment" in event)) return;
    const a = event.appointment;
    const newStart = new Date(start);
    const newEnd = new Date(end);
    const targetColumn = view === "day" && resourceId !== undefined ? String(resourceId) : null;

    if (targetColumn === UNASSIGNED && a.providerId) {
      showToast("Appointments can't be moved to Unassigned -- drop it on a provider's column.");
      return;
    }
    const providerId = targetColumn && targetColumn !== UNASSIGNED && targetColumn !== a.providerId ? targetColumn : null;
    if (!providerId && newStart.getTime() === event.start.getTime() && newEnd.getTime() === event.end.getTime()) return;

    // Moving to another provider: take the location they're working at
    // then, if their hours say (the server still checks they work there).
    let locationDetailId: string | null = null;
    if (providerId) {
      const window_ = columns
        .find((c) => c.provider.id === providerId)
        ?.workingHours.find((w) => atTime(newStart, w.start) <= newStart && newStart < atTime(newStart, w.end));
      if (window_?.locationId && window_.locationId !== a.locationId) locationDetailId = window_.locationId;
    }

    setPendingMove({
      appointment: a,
      request: {
        startsAt: wallClockToIso(newStart, tz),
        endsAt: wallClockToIso(newEnd, tz),
        providerId,
        locationDetailId,
        roomId: null,
      },
    });
  }
  const slotMinutes = settings.slotMinutes === 15 ? 15 : 30;
  const columnCount = resources?.length ?? 1;

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-text">Schedule</h1>

      <ScheduleToolbar
        settings={settings}
        view={view}
        dateKey={dateKey}
        title={title}
        filters={filters}
        onToday={() => update({ date: todayKey(tz), page: "" })}
        onStep={step}
        onDateChange={(key) => update({ date: key, page: "" })}
        onViewChange={(v) => update({ view: v, page: "" })}
        listRange={listRange}
        onListRangeChange={(r) => update({ listRange: r === "day" ? "" : r, page: "" })}
        onFiltersChange={onFiltersChange}
        onNewAppointment={() =>
          setNewAppointment({ date: dateKey, providerId: filters.providerId || undefined, locationId: filters.locationId || undefined })
        }
      />

      {selectedProvider && (
        <div className="flex flex-wrap items-center gap-2 rounded-md bg-primary-light/60 px-3 py-2 text-sm text-text" role="status">
          <span>
            Showing <strong>{selectedProvider.name}</strong>
            {selectedProvider.credentials ? `, ${selectedProvider.credentials}` : ""} only — switch Day, Week, Month, Year, or List to see their schedule over time.
          </span>
          <button type="button" className="btn-secondary px-2 py-0.5 text-xs" onClick={() => onFiltersChange({ providerId: "" })}>
            ✕ Show all providers
          </button>
        </div>
      )}

      {activeQuery.isError && <p className="alert-error">Could not load appointments: {activeQuery.error.message}</p>}

      {view === "year" && (filters.status || filters.appointmentTypeId || filters.patient) && (
        <p className="text-sm text-text-muted">The year view counts every non-cancelled appointment; the status, type, and patient filters apply to the other views.</p>
      )}

      {view === "year" && (
        <div className={countsQuery.isFetching ? "opacity-70" : ""}>
          <YearView year={year} counts={yearCounts} todayKey={todayKey(tz)} onPickDate={(key) => update({ view: "day", date: key })} />
        </div>
      )}

      {view === "list" && (
        <div className={`card p-0 ${listQuery.isFetching ? "opacity-70" : ""}`}>
          <ListView
            data={listQuery.data}
            isLoading={listQuery.isLoading}
            timezone={tz}
            canManage={settings.canCreate}
            canReschedule={settings.canReschedule}
            onPage={(p) => update({ page: p > 1 ? String(p) : "" })}
            onSelect={setSelected}
            onReschedule={setRescheduling}
          />
        </div>
      )}

      {isCalendar && (
      <div className="card overflow-x-auto p-0">
        <div
          className={`schedule-calendar ${view === "month" ? "h-[78vh]" : "h-[72vh]"} ${activeQuery.isFetching ? "opacity-70" : ""}`}
          style={{ minWidth: view === "day" ? Math.max(360, 64 + columnCount * 190) : view === "week" ? 760 : 640 }}
        >
          <DnDCalendar
            localizer={localizer}
            toolbar={false}
            view={view as View}
            views={["day", "week", "month"]}
            onView={(v) => update({ view: v })}
            date={current}
            onNavigate={(d) => update({ date: toDateKey(d) })}
            onDrillDown={(d) => update({ view: "day", date: toDateKey(d) })}
            events={events}
            backgroundEvents={blocks}
            resources={resources}
            resourceIdAccessor="id"
            resourceTitleAccessor="title"
            step={slotMinutes}
            timeslots={60 / slotMinutes}
            min={min}
            max={max}
            scrollToTime={min}
            formats={formats}
            components={components}
            eventPropGetter={eventPropGetter}
            slotPropGetter={slotPropGetter}
            onSelectEvent={(e) => "appointment" in e && setSelected(e.appointment)}
            tooltipAccessor={(e) =>
              "appointment" in e
                ? `${formatTime(e.start)} ${e.appointment.patientName} · ${e.appointment.appointmentTypeName ?? ""}`
                : e.title
            }
            popup
            dayLayoutAlgorithm="no-overlap"
            draggableAccessor={canDrag}
            resizableAccessor={canDrag}
            resizable
            onEventDrop={handleDrop}
            onEventResize={handleDrop}
            selectable={settings.canCreate && view !== "month" ? "ignoreEvents" : false}
            onSelectSlot={handleSelectSlot}
          />
        </div>
      </div>
      )}

      {view === "day" && dayQuery.data && (
        <ProviderDailySummary
          title={dateKey === todayKey(tz) ? "Today's PT schedule" : `PT schedule · ${title}`}
          columns={columns}
          appointments={dayQuery.data.appointments}
          timezone={tz}
          onSelectAppointment={setSelected}
        />
      )}

      {selected && (
        <AppointmentDetailsPanel
          key={selected.id}
          appointment={selected}
          timezone={tz}
          canManage={settings.canCreate}
          canReschedule={settings.canReschedule}
          onReschedule={() => {
            setRescheduling(selected);
            setSelected(null);
          }}
          onClose={closeDetails}
        />
      )}

      {rescheduling && (
        <RescheduleDialog appointment={rescheduling} settings={settings} timezone={tz} onClose={() => setRescheduling(null)} />
      )}

      {newAppointment && (
        <NewAppointmentDialog settings={settings} timezone={tz} defaults={newAppointment} onClose={() => setNewAppointment(null)} />
      )}

      {pendingMove && (
        <MoveAppointmentDialog
          appointment={pendingMove.appointment}
          request={pendingMove.request}
          timezone={tz}
          onClose={() => setPendingMove(null)}
        />
      )}
    </div>
  );
}

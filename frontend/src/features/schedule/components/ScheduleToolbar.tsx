import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AppointmentStatus, AppointmentStatusLabels, type ListRange, type ScheduleFilters, type ScheduleSettings, type ScheduleView } from "../types";

interface Props {
  settings: ScheduleSettings;
  view: ScheduleView;
  dateKey: string;
  title: string;
  filters: ScheduleFilters;
  onToday: () => void;
  onStep: (direction: -1 | 1) => void;
  onDateChange: (dateKey: string) => void;
  onViewChange: (view: ScheduleView) => void;
  onFiltersChange: (changes: Partial<ScheduleFilters>) => void;
  onNewAppointment: () => void;
  listRange: ListRange;
  onListRangeChange: (range: ListRange) => void;
}

const views: { id: ScheduleView; label: string }[] = [
  { id: "day", label: "Day" },
  { id: "week", label: "Week" },
  { id: "month", label: "Month" },
  { id: "year", label: "Year" },
  { id: "list", label: "List" },
];

const statusOptions = [
  AppointmentStatus.Scheduled,
  AppointmentStatus.Confirmed,
  AppointmentStatus.CheckedIn,
  AppointmentStatus.InProgress,
  AppointmentStatus.Completed,
  AppointmentStatus.Cancelled,
  AppointmentStatus.NoShow,
];

/** Date navigation, view switcher, and filters. Filters collapse behind a
 * toggle on small screens so the calendar keeps the space. */
export function ScheduleToolbar(props: Props) {
  const { settings, view, dateKey, title, filters, onFiltersChange } = props;
  const [patientText, setPatientText] = useState(filters.patient);
  const [filtersOpen, setFiltersOpen] = useState(false);

  // Debounce patient search so typing doesn't fire a request per keystroke.
  useEffect(() => {
    if (patientText === filters.patient) return;
    const timer = window.setTimeout(() => onFiltersChange({ patient: patientText.trim() }), 300);
    return () => window.clearTimeout(timer);
  }, [patientText, filters.patient, onFiltersChange]);

  const providers = settings.providers.filter(
    (p) => p.isActive && (!filters.locationId || p.locationIds.includes(filters.locationId)),
  );
  const activeFilterCount = [filters.providerId, filters.status, filters.appointmentTypeId, filters.patient].filter(Boolean).length;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className="btn-secondary" onClick={props.onToday}>
          Today
        </button>
        <div className="flex">
          <button type="button" className="btn-secondary rounded-r-none" onClick={() => props.onStep(-1)} aria-label="Previous">
            ‹ Previous
          </button>
          <button type="button" className="btn-secondary -ml-px rounded-l-none" onClick={() => props.onStep(1)} aria-label="Next">
            Next ›
          </button>
        </div>
        <input
          type="date"
          aria-label="Go to date"
          className="field-input w-auto py-1.5"
          value={dateKey}
          onChange={(e) => e.target.value && props.onDateChange(e.target.value)}
        />
        <h2 className="min-w-0 flex-1 truncate text-base font-semibold text-text sm:text-lg">{title}</h2>
        <div role="tablist" aria-label="Calendar view" className="flex rounded-md border border-border bg-surface p-0.5">
          {views.map((v) => (
            <button
              key={v.id}
              type="button"
              role="tab"
              aria-selected={view === v.id}
              onClick={() => props.onViewChange(v.id)}
              className={`rounded px-3 py-1 text-sm font-medium transition ${
                view === v.id ? "bg-primary text-white" : "text-text-muted hover:text-primary"
              }`}
            >
              {v.label}
            </button>
          ))}
        </div>
        {view === "list" && (
          <select
            aria-label="List covers"
            className="field-input w-auto py-1.5"
            value={props.listRange}
            onChange={(e) => props.onListRangeChange(e.target.value as ListRange)}
          >
            <option value="day">Day</option>
            <option value="week">Week</option>
            <option value="month">Month</option>
          </select>
        )}
        <Link
          to={filters.providerId ? `/schedule/hours?provider=${filters.providerId}` : "/schedule/hours"}
          className="btn-secondary"
        >
          Provider hours
        </Link>
        {settings.canCreate && (
          <button type="button" className="btn-primary" onClick={props.onNewAppointment}>
            + New appointment
          </button>
        )}
      </div>

      <button type="button" className="btn-secondary md:hidden" onClick={() => setFiltersOpen((o) => !o)} aria-expanded={filtersOpen}>
        Filters{activeFilterCount > 0 ? ` (${activeFilterCount})` : ""}
      </button>
      <div className={`${filtersOpen ? "grid" : "hidden"} grid-cols-1 gap-2 sm:grid-cols-2 md:grid md:grid-cols-5`}>
        <select
          aria-label="Location"
          className="field-input py-1.5"
          value={filters.locationId}
          onChange={(e) => onFiltersChange({ locationId: e.target.value, providerId: "" })}
        >
          <option value="">All locations</option>
          {settings.locations.map((l) => (
            <option key={l.id} value={l.id}>
              {l.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Provider"
          className="field-input py-1.5"
          value={filters.providerId}
          onChange={(e) => onFiltersChange({ providerId: e.target.value })}
        >
          <option value="">All providers</option>
          {providers.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
              {p.credentials ? `, ${p.credentials}` : ""}
            </option>
          ))}
        </select>
        <select
          aria-label="Appointment status"
          className="field-input py-1.5"
          value={filters.status}
          onChange={(e) => onFiltersChange({ status: e.target.value })}
        >
          <option value="">All statuses</option>
          {statusOptions.map((s) => (
            <option key={s} value={String(s)}>
              {AppointmentStatusLabels[s]}
            </option>
          ))}
        </select>
        <select
          aria-label="Appointment type"
          className="field-input py-1.5"
          value={filters.appointmentTypeId}
          onChange={(e) => onFiltersChange({ appointmentTypeId: e.target.value })}
        >
          <option value="">All appointment types</option>
          {settings.appointmentTypes.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <input
          type="search"
          aria-label="Search patient"
          placeholder="Search patient name or MRN"
          className="field-input py-1.5"
          value={patientText}
          onChange={(e) => setPatientText(e.target.value)}
        />
      </div>
    </div>
  );
}

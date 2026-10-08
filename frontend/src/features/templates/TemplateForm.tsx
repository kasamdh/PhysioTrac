import { Fragment, type ReactNode } from "react";
import { PainScale } from "../charting/components/PainScale";
import { fieldError, isVisible, valueAsList } from "./rules";
import {
  ComponentLabels,
  FieldType,
  type FieldValue,
  type FieldValues,
  type TemplateField,
  type TemplateSection,
} from "./types";

export interface TemplateFormProps {
  values: FieldValues;
  /** The note's narrative text by column, for fields stored there. */
  columns: Record<string, string>;
  readOnly?: boolean;
  onChange?: (value: FieldValue) => void;
  onColumnChange?: (column: string, text: string) => void;
  /** Renders a section's built-in clinical component; a labelled
   * placeholder is shown when the host has none. */
  renderComponent?: (component: string, section: TemplateSection) => ReactNode;
  /** Labels to flag as missing (e.g. after a failed sign attempt). */
  missing?: ReadonlySet<string>;
  /** Extra controls shown full width below a field (e.g. AI drafting). */
  renderFieldExtra?: (field: TemplateField) => ReactNode;
}

/** Renders any template version: no clinical field is hard-coded here --
 * the template decides the sections, fields, types, choices and rules. */
export function TemplateForm({
  sections,
  ...props
}: TemplateFormProps & { sections: TemplateSection[] }) {
  return (
    <div className="space-y-5">
      {sections.map((s) => (
        <section
          key={s.key}
          aria-labelledby={`tpl-${s.key}`}
          className="space-y-4 rounded-lg border border-border bg-white p-4 sm:p-5"
        >
          <h2 id={`tpl-${s.key}`} className="text-2xl font-bold text-[#1565b8]">
            {s.title}
          </h2>
          <TemplateSectionFields section={s} {...props} />
        </section>
      ))}
    </div>
  );
}

/** One section's help text, clinical component and fields. */
export function TemplateSectionFields({
  section,
  values,
  columns,
  readOnly = false,
  onChange,
  onColumnChange,
  renderComponent,
  missing,
  renderFieldExtra,
}: TemplateFormProps & { section: TemplateSection }) {
  return (
    <div className="space-y-4">
      {section.helpText && (
        <p className="text-text-muted">{section.helpText}</p>
      )}
      {section.component &&
        (renderComponent?.(section.component, section) ?? (
          <p className="rounded-md border border-dashed border-border bg-surface-muted px-3 py-2 text-text-muted">
            {ComponentLabels[section.component] ?? section.component} — recorded
            in the encounter.
          </p>
        ))}
      <div className="grid gap-4 md:grid-cols-2">
        {section.fields
          .filter((f) => isVisible(f, values))
          .map((f) => {
            const extra = renderFieldExtra?.(f);
            return (
              <Fragment key={f.key}>
                <FieldInput
                  field={f}
                  value={values[f.key]}
                  columnText={
                    f.noteColumn ? (columns[f.noteColumn] ?? "") : undefined
                  }
                  readOnly={readOnly}
                  missing={missing?.has(f.label) ?? false}
                  onChange={(v) => onChange?.(v)}
                  onColumnChange={(t) =>
                    f.noteColumn && onColumnChange?.(f.noteColumn, t)
                  }
                />
                {extra && <div className="md:col-span-2">{extra}</div>}
              </Fragment>
            );
          })}
      </div>
    </div>
  );
}

const wide = new Set<FieldType>([
  FieldType.LongText,
  FieldType.StructuredTable,
  FieldType.Multiselect,
  FieldType.PainScale,
  FieldType.Radio,
  FieldType.Signature,
]);

function FieldInput({
  field: f,
  value,
  columnText,
  readOnly,
  missing,
  onChange,
  onColumnChange,
}: {
  field: TemplateField;
  value: FieldValue | undefined;
  columnText: string | undefined;
  readOnly: boolean;
  missing: boolean;
  onChange: (v: FieldValue) => void;
  onColumnChange: (text: string) => void;
}) {
  const id = `fld-${f.key}`;
  const error = fieldError(f, value);
  const set = (patch: Omit<FieldValue, "key">) =>
    onChange({ key: f.key, ...patch });
  const describedBy =
    [f.helpText && `${id}-help`, error && `${id}-err`]
      .filter(Boolean)
      .join(" ") || undefined;

  const label = (
    <span className="font-bold text-[#333]">
      {f.label}
      {f.isRequired && (
        <span className="ml-1 text-danger" aria-hidden="true">
          *
        </span>
      )}
      {f.unit && (
        <span className="ml-1 font-normal text-text-muted">({f.unit})</span>
      )}
    </span>
  );
  const common = {
    id,
    readOnly,
    "aria-required": f.isRequired || undefined,
    "aria-invalid": !!error || missing || undefined,
    "aria-describedby": describedBy,
    placeholder: readOnly ? "" : (f.placeholder ?? ""),
  };

  let control: ReactNode;
  switch (f.fieldType) {
    case FieldType.ShortText:
      control = (
        <input
          {...common}
          className="field-input mt-1"
          value={f.noteColumn ? (columnText ?? "") : (value?.text ?? "")}
          onChange={(e) =>
            f.noteColumn
              ? onColumnChange(e.target.value)
              : set({ text: e.target.value })
          }
        />
      );
      break;
    case FieldType.LongText:
      control = (
        <textarea
          {...common}
          className="field-input mt-1 min-h-[6rem] resize-y"
          value={f.noteColumn ? (columnText ?? "") : (value?.text ?? "")}
          onChange={(e) =>
            f.noteColumn
              ? onColumnChange(e.target.value)
              : set({ text: e.target.value })
          }
        />
      );
      break;
    case FieldType.Number:
    case FieldType.ClinicalMeasurement:
      control = (
        <input
          {...common}
          type="number"
          inputMode="decimal"
          step="any"
          min={f.validation?.min ?? undefined}
          max={f.validation?.max ?? undefined}
          className="field-input mt-1"
          value={value?.number ?? ""}
          onChange={(e) =>
            set({
              number: e.target.value === "" ? null : Number(e.target.value),
            })
          }
        />
      );
      break;
    case FieldType.Date:
      control = (
        <input
          {...common}
          type="date"
          className="field-input mt-1"
          value={value?.date ?? ""}
          onChange={(e) => set({ date: e.target.value || null })}
        />
      );
      break;
    case FieldType.Time:
      control = (
        <input
          {...common}
          type="time"
          className="field-input mt-1"
          value={value?.time ?? ""}
          onChange={(e) => set({ time: e.target.value || null })}
        />
      );
      break;
    case FieldType.Select:
      control = (
        <select
          id={id}
          disabled={readOnly}
          aria-required={f.isRequired || undefined}
          aria-describedby={describedBy}
          className="field-input mt-1"
          value={value?.text ?? ""}
          onChange={(e) => set({ text: e.target.value || null })}
        >
          <option value="">—</option>
          {(f.options ?? []).map((o) => (
            <option key={o}>{o}</option>
          ))}
        </select>
      );
      break;
    case FieldType.PainScale:
      return (
        <div className="md:col-span-2">
          <PainScale
            label={f.label + (f.isRequired ? " *" : "")}
            value={value?.number ?? null}
            readOnly={readOnly}
            onChange={(n) => set({ number: n })}
          />
          <Help id={id} field={f} error={error} missing={missing} />
        </div>
      );
    case FieldType.Checkbox:
      return (
        <div>
          <label className="flex min-h-11 items-center gap-3">
            <input
              id={id}
              type="checkbox"
              className="h-5 w-5"
              disabled={readOnly}
              checked={value?.bool === true}
              aria-describedby={describedBy}
              onChange={(e) => set({ bool: e.target.checked })}
            />
            {label}
          </label>
          <Help id={id} field={f} error={error} missing={missing} />
        </div>
      );
    case FieldType.Radio:
    case FieldType.Multiselect: {
      const multi = f.fieldType === FieldType.Multiselect;
      const picked = valueAsList(value);
      return (
        <div className="md:col-span-2">
          <span id={`${id}-label`}>{label}</span>
          <div
            role={multi ? "group" : "radiogroup"}
            aria-labelledby={`${id}-label`}
            aria-describedby={describedBy}
            className="mt-1 flex flex-wrap gap-2"
          >
            {(f.options ?? []).map((o) => {
              const on = picked.includes(o);
              return (
                <button
                  key={o}
                  type="button"
                  disabled={readOnly}
                  className="seg-btn min-h-11 rounded border border-border px-3"
                  role={multi ? undefined : "radio"}
                  aria-checked={multi ? undefined : on}
                  aria-pressed={multi ? on : undefined}
                  onClick={() =>
                    multi
                      ? set({
                          json: JSON.stringify(
                            on ? picked.filter((p) => p !== o) : [...picked, o],
                          ),
                        })
                      : set({ text: on ? null : o })
                  }
                >
                  {o}
                </button>
              );
            })}
          </div>
          <Help id={id} field={f} error={error} missing={missing} />
        </div>
      );
    }
    case FieldType.StructuredTable:
      return (
        <div className="md:col-span-2">
          {label}
          <TableInput
            field={f}
            value={value}
            readOnly={readOnly}
            onChange={(json) => set({ json })}
          />
          <Help id={id} field={f} error={error} missing={missing} />
        </div>
      );
    case FieldType.Signature:
      return (
        <div className="md:col-span-2 rounded-md border border-dashed border-border px-3 py-2 text-text-muted">
          <span className="font-bold text-[#333]">{f.label}</span> — applied
          electronically when the note is signed.
        </div>
      );
  }

  return (
    <label
      htmlFor={id}
      className={`block ${wide.has(f.fieldType) ? "md:col-span-2" : ""}`}
    >
      {label}
      {control}
      <Help id={id} field={f} error={error} missing={missing} />
    </label>
  );
}

function Help({
  id,
  field,
  error,
  missing,
}: {
  id: string;
  field: TemplateField;
  error: string | null;
  missing: boolean;
}) {
  return (
    <>
      {field.helpText && (
        <span id={`${id}-help`} className="mt-1 block text-text-muted">
          {field.helpText}
        </span>
      )}
      {error && (
        <span id={`${id}-err`} role="alert" className="mt-1 block text-danger">
          {error}
        </span>
      )}
      {!error && missing && (
        <span className="mt-1 block text-danger">Required before signing.</span>
      )}
    </>
  );
}

type Row = Record<string, string>;

function TableInput({
  field,
  value,
  readOnly,
  onChange,
}: {
  field: TemplateField;
  value: FieldValue | undefined;
  readOnly: boolean;
  onChange: (json: string) => void;
}) {
  const columns = field.columns ?? [];
  let rows: Row[] = [];
  try {
    const parsed: unknown = value?.json ? JSON.parse(value.json) : [];
    rows = Array.isArray(parsed) ? (parsed as Row[]) : [];
  } catch {
    rows = [];
  }
  const save = (next: Row[]) => onChange(JSON.stringify(next));
  return (
    <div className="mt-1">
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c.key}>{c.label}</th>
            ))}
            {!readOnly && <th className="cell-menu" aria-label="Remove" />}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && (
            <tr>
              <td colSpan={columns.length + 1} className="text-text-muted">
                No rows.
              </td>
            </tr>
          )}
          {rows.map((r, i) => (
            <tr key={i}>
              {columns.map((c) => (
                <td key={c.key} data-label={c.label}>
                  {readOnly ? (
                    (r[c.key] ?? "")
                  ) : (
                    <input
                      aria-label={`${field.label} row ${i + 1} ${c.label}`}
                      className="field-input"
                      value={r[c.key] ?? ""}
                      onChange={(e) =>
                        save(
                          rows.map((x, j) =>
                            j === i ? { ...x, [c.key]: e.target.value } : x,
                          ),
                        )
                      }
                    />
                  )}
                </td>
              ))}
              {!readOnly && (
                <td data-label="">
                  <button
                    type="button"
                    className="btn-refresh"
                    aria-label={`Remove row ${i + 1}`}
                    onClick={() => save(rows.filter((_, j) => j !== i))}
                  >
                    ×
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
      {!readOnly && (
        <button
          type="button"
          className="btn-refresh mt-2"
          onClick={() => save([...rows, {}])}
        >
          + Add row
        </button>
      )}
    </div>
  );
}

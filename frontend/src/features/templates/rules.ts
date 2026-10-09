import {
  FieldType,
  type FieldValue,
  type FieldValues,
  type TemplateField,
} from "./types";

/** Client mirror of TemplateRules (the server's check is authoritative). */

export function valueAsText(v: FieldValue | undefined): string | null {
  if (!v) return null;
  if (v.text != null && v.text.trim() !== "") return v.text;
  if (v.number != null) return String(v.number);
  if (v.bool != null) return v.bool ? "true" : "false";
  if (v.date) return v.date;
  if (v.time) return v.time;
  if (v.json && v.json !== "[]" && v.json !== "null") return v.json;
  return null;
}

export function valueAsList(v: FieldValue | undefined): string[] {
  if (v?.json != null) {
    try {
      const parsed: unknown = JSON.parse(v.json);
      return Array.isArray(parsed)
        ? parsed.filter((x): x is string => typeof x === "string")
        : [];
    } catch {
      return [];
    }
  }
  const t = valueAsText(v);
  return t == null ? [] : [t];
}

/** Whether a conditional field is shown for the current values. */
export function isVisible(field: TemplateField, values: FieldValues): boolean {
  const c = field.condition;
  if (!c) return true;
  const list = valueAsList(values[c.field]).map((x) => x.toLowerCase());
  if (c.notEmpty && list.length === 0) return false;
  if (c.value != null && !list.includes(c.value.toLowerCase())) return false;
  if (
    c.anyOf?.length &&
    !list.some((x) => c.anyOf!.some((a) => a.toLowerCase() === x))
  )
    return false;
  return true;
}

/** Labels of required, visible fields with no value. `columns` holds the
 * note's narrative text for fields stored in a note column. */
export function missingRequired(
  fields: TemplateField[],
  values: FieldValues,
  columns: Record<string, string>,
): string[] {
  return fields
    .filter(
      (f) =>
        f.isRequired &&
        f.fieldType !== FieldType.Signature &&
        isVisible(f, values),
    )
    .filter((f) => {
      if (f.noteColumn) return !(columns[f.noteColumn] ?? "").trim();
      const v = values[f.key];
      if (f.fieldType === FieldType.Checkbox) return v?.bool !== true;
      if (
        f.fieldType === FieldType.Multiselect ||
        f.fieldType === FieldType.StructuredTable
      ) {
        try {
          const parsed: unknown = v?.json ? JSON.parse(v.json) : [];
          return !Array.isArray(parsed) || parsed.length === 0;
        } catch {
          return true;
        }
      }
      return valueAsText(v) == null;
    })
    .map((f) => f.label);
}

/** A problem with one field's value, or null (mirrors ValidateValues). */
export function fieldError(field: TemplateField, v: FieldValue | undefined) {
  if (!v) return null;
  const rules = field.validation;
  switch (field.fieldType) {
    case FieldType.ShortText:
    case FieldType.LongText: {
      const text = v.text ?? "";
      const max =
        rules?.maxLength ??
        (field.fieldType === FieldType.ShortText ? 500 : 20000);
      if (text.length > max) return `${max} characters at most.`;
      if (rules?.pattern && text) {
        try {
          if (!new RegExp(rules.pattern).test(text))
            return rules.patternMessage ?? "Not in the expected format.";
        } catch {
          return null;
        }
      }
      return null;
    }
    case FieldType.Number:
    case FieldType.ClinicalMeasurement:
    case FieldType.PainScale: {
      if (v.number == null) return null;
      const pain = field.fieldType === FieldType.PainScale;
      const min = pain ? (field.scaleMin ?? 0) : rules?.min;
      const max = pain ? (field.scaleMax ?? 10) : rules?.max;
      if (min != null && v.number < min) return `Must be at least ${min}.`;
      if (max != null && v.number > max) return `Must be at most ${max}.`;
      return null;
    }
    default:
      return null;
  }
}

/** Turns a label into a field/section key: "Pain at rest" -> "painAtRest". */
export function toKey(label: string, taken: ReadonlySet<string> = new Set()) {
  const words = label
    .replace(/[^a-zA-Z0-9 ]/g, " ")
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  let key =
    words
      .map((w, i) =>
        i === 0
          ? w.toLowerCase()
          : w[0].toUpperCase() + w.slice(1).toLowerCase(),
      )
      .join("")
      .replace(/^[0-9]+/, "") || "field";
  key = key.slice(0, 70);
  let n = 2;
  const base = key;
  while (taken.has(key)) key = `${base}${n++}`;
  return key;
}

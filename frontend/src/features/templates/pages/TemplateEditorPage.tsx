import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router-dom";
import { useToast } from "../../../components/Toast";
import { ApiError, apiRequest } from "../../../lib/apiClient";
import { AdminPageHeader } from "../../admin/AdminPageHeader";
import { useAuth } from "../../auth/AuthProvider";
import { RoleSets, canAccess } from "../../auth/permissions";
import { NoteTypeLabels, TEMPLATE_NOTE_TYPES } from "../../workflow/types";
import {
  copyTemplate,
  createTemplate,
  fetchTemplate,
  fetchTemplateVersions,
  updateTemplate,
} from "../api";
import { toKey } from "../rules";
import { TemplateForm } from "../TemplateForm";
import {
  ComponentLabels,
  FieldType,
  FieldTypeLabels,
  NoteColumnLabels,
  OPTION_TYPES,
  SpecialtyLabels,
  TEXT_TYPES,
  type DocTemplateDetail,
  type FieldValues,
  type SaveTemplateRequest,
  type TemplateField,
  type TemplateSection,
} from "../types";

const emptyDraft = (): SaveTemplateRequest => ({
  name: "",
  noteType: 1,
  specialty: 0,
  description: null,
  appointmentTypeIds: [],
  changeSummary: null,
  sections: [{ key: "section1", title: "Section 1", fields: [] }],
});

const fromDetail = (d: DocTemplateDetail): SaveTemplateRequest => ({
  name: d.template.name,
  noteType: d.template.noteType,
  specialty: d.template.specialty,
  description: d.template.description,
  appointmentTypeIds: d.template.appointmentTypeIds,
  changeSummary: null,
  sections: d.currentVersion.sections,
});

interface AppointmentTypeOption {
  id: string;
  name: string;
  isActive: boolean;
}

/** Create or edit a documentation template: details, sections and fields,
 * a live preview, and the version history. Saving changes to fields
 * publishes a new version; notes keep the version they were written with. */
export function TemplateEditorPage() {
  const { templateId } = useParams();
  const isNew = !templateId;
  const detail = useQuery({
    queryKey: ["templates", "detail", templateId],
    queryFn: () => fetchTemplate(templateId!),
    enabled: !isNew,
  });
  if (!isNew && detail.isLoading)
    return <p className="text-text-muted">Loading…</p>;
  if (detail.isError)
    return <p className="alert-error">{detail.error.message}</p>;
  return (
    <Editor
      key={detail.data?.currentVersion.id ?? "new"}
      detail={detail.data ?? null}
    />
  );
}

function Editor({ detail }: { detail: DocTemplateDetail | null }) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const canManage = canAccess(user, RoleSets.OrganizationAdministration);
  const readOnly = !canManage || !!detail?.template.isSystem;
  const [draft, setDraft] = useState<SaveTemplateRequest>(() =>
    detail ? fromDetail(detail) : emptyDraft(),
  );
  const [tab, setTab] = useState<"design" | "preview" | "versions">("design");
  const [errors, setErrors] = useState<string[]>([]);
  const [preview, setPreview] = useState<FieldValues>({});
  const [previewColumns, setPreviewColumns] = useState<Record<string, string>>(
    {},
  );

  const types = useQuery({
    queryKey: ["appointment-types", "all"],
    queryFn: () =>
      apiRequest<AppointmentTypeOption[]>("/api/v1/appointment-types"),
    enabled: canManage,
  });
  const versions = useQuery({
    queryKey: ["templates", "versions", detail?.template.id],
    queryFn: () => fetchTemplateVersions(detail!.template.id),
    enabled: !!detail && tab === "versions",
  });

  const save = useMutation({
    mutationFn: () =>
      detail
        ? updateTemplate(detail.template.id, draft)
        : createTemplate(draft),
    onSuccess: (saved) => {
      setErrors([]);
      void queryClient.invalidateQueries({ queryKey: ["templates"] });
      showToast(
        detail &&
          saved.currentVersion.versionNumber !==
            detail.currentVersion.versionNumber
          ? `Saved as version ${saved.currentVersion.versionNumber}.`
          : "Template saved.",
      );
      navigate(`/admin/templates/${saved.template.id}`, { replace: true });
    },
    onError: (e: Error) => {
      const payload =
        e instanceof ApiError
          ? (e.payload as { errors?: string[] } | undefined)
          : undefined;
      setErrors(payload?.errors?.length ? payload.errors : [e.message]);
    },
  });
  const copy = useMutation({
    mutationFn: () => copyTemplate(detail!.template.id),
    onSuccess: (d) => {
      showToast(`Copied as ${d.template.name}.`);
      void queryClient.invalidateQueries({ queryKey: ["templates"] });
      navigate(`/admin/templates/${d.template.id}`);
    },
    onError: (e: Error) => showToast(e.message),
  });

  const allKeys = new Set(
    draft.sections.flatMap((s) => s.fields.map((f) => f.key)),
  );
  const setSection = (i: number, s: TemplateSection) =>
    setDraft((d) => ({
      ...d,
      sections: d.sections.map((x, j) => (j === i ? s : x)),
    }));
  const moveSection = (i: number, by: number) =>
    setDraft((d) => {
      const next = [...d.sections];
      const [s] = next.splice(i, 1);
      next.splice(i + by, 0, s);
      return { ...d, sections: next };
    });

  return (
    <div className="pb-10">
      <AdminPageHeader
        title={
          detail
            ? `${detail.template.name} — v${detail.currentVersion.versionNumber}`
            : "New documentation template"
        }
        back={{ to: "/admin/templates", label: "Documentation Templates" }}
        actions={
          <>
            {detail && canManage && (
              <button
                type="button"
                className="btn-refresh"
                disabled={copy.isPending}
                onClick={() => copy.mutate()}
              >
                {detail.template.isSystem ? "Copy to customize" : "Copy"}
              </button>
            )}
            {!readOnly && (
              <button
                type="button"
                className="btn-primary"
                disabled={save.isPending}
                onClick={() => save.mutate()}
              >
                {save.isPending
                  ? "Saving…"
                  : detail
                    ? "Save"
                    : "Create template"}
              </button>
            )}
          </>
        }
      />

      {detail?.template.isSystem && (
        <p className="mb-4 rounded-md border border-border bg-surface-muted px-3 py-2 text-[#333]">
          This is a system template. It can’t be changed; copy it to make your
          clinic’s own version.
        </p>
      )}
      {errors.length > 0 && (
        <div role="alert" className="alert-error mb-4">
          <p className="font-bold">The template can’t be saved yet:</p>
          <ul className="ml-5 list-disc">
            {errors.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        </div>
      )}

      <div className="mb-4 grid gap-4 rounded-lg border border-border bg-white p-4 md:grid-cols-2">
        <label className="block">
          <span className="font-bold text-[#333]">Name</span>
          <input
            className="field-input mt-1"
            readOnly={readOnly}
            maxLength={200}
            value={draft.name}
            onChange={(e) => setDraft({ ...draft, name: e.target.value })}
          />
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block">
            <span className="font-bold text-[#333]">Note type</span>
            <select
              className="field-input mt-1"
              disabled={readOnly}
              value={draft.noteType}
              onChange={(e) =>
                setDraft({ ...draft, noteType: Number(e.target.value) })
              }
            >
              {TEMPLATE_NOTE_TYPES.map((t) => (
                <option key={t} value={t}>
                  {NoteTypeLabels[t]}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="font-bold text-[#333]">Specialty</span>
            <select
              className="field-input mt-1"
              disabled={readOnly}
              value={draft.specialty}
              onChange={(e) =>
                setDraft({ ...draft, specialty: Number(e.target.value) })
              }
            >
              {Object.entries(SpecialtyLabels).map(([v, l]) => (
                <option key={v} value={v}>
                  {l}
                </option>
              ))}
            </select>
          </label>
        </div>
        <label className="block md:col-span-2">
          <span className="font-bold text-[#333]">Description</span>
          <input
            className="field-input mt-1"
            readOnly={readOnly}
            maxLength={1000}
            value={draft.description ?? ""}
            onChange={(e) =>
              setDraft({ ...draft, description: e.target.value || null })
            }
          />
        </label>
        {canManage && !!types.data?.length && (
          <fieldset className="md:col-span-2">
            <legend className="font-bold text-[#333]">
              Suggest for these appointment types
            </legend>
            <div className="mt-1 flex flex-wrap gap-x-5 gap-y-1">
              {types.data
                .filter((t) => t.isActive)
                .map((t) => (
                  <label
                    key={t.id}
                    className="flex min-h-11 items-center gap-2"
                  >
                    <input
                      type="checkbox"
                      disabled={readOnly}
                      checked={draft.appointmentTypeIds.includes(t.id)}
                      onChange={(e) =>
                        setDraft({
                          ...draft,
                          appointmentTypeIds: e.target.checked
                            ? [...draft.appointmentTypeIds, t.id]
                            : draft.appointmentTypeIds.filter(
                                (x) => x !== t.id,
                              ),
                        })
                      }
                    />
                    {t.name}
                  </label>
                ))}
            </div>
          </fieldset>
        )}
        {detail && !readOnly && (
          <label className="block md:col-span-2">
            <span className="font-bold text-[#333]">What changed</span>
            <span className="ml-2 text-text-muted">
              — shown in the version history
            </span>
            <input
              className="field-input mt-1"
              maxLength={500}
              value={draft.changeSummary ?? ""}
              onChange={(e) =>
                setDraft({ ...draft, changeSummary: e.target.value || null })
              }
            />
          </label>
        )}
      </div>

      <div className="seg-group mb-4" role="tablist" aria-label="Template view">
        {(
          [
            ["design", readOnly ? "Sections & fields" : "Design"],
            ["preview", "Preview"],
            ...(detail ? ([["versions", "Version history"]] as const) : []),
          ] as const
        ).map(([v, label]) => (
          <button
            key={v}
            type="button"
            role="tab"
            className="seg-btn"
            aria-selected={tab === v}
            onClick={() => setTab(v)}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "design" && (
        <div className="space-y-4">
          {draft.sections.map((s, i) => (
            <SectionEditor
              key={i}
              section={s}
              index={i}
              count={draft.sections.length}
              readOnly={readOnly}
              allFields={draft.sections.flatMap((x) => x.fields)}
              allKeys={allKeys}
              onChange={(next) => setSection(i, next)}
              onMove={(by) => moveSection(i, by)}
              onRemove={() =>
                setDraft((d) => ({
                  ...d,
                  sections: d.sections.filter((_, j) => j !== i),
                }))
              }
            />
          ))}
          {!readOnly && (
            <button
              type="button"
              className="btn-refresh"
              onClick={() =>
                setDraft((d) => {
                  const keys = new Set(d.sections.map((s) => s.key));
                  const title = `Section ${d.sections.length + 1}`;
                  return {
                    ...d,
                    sections: [
                      ...d.sections,
                      { key: toKey(title, keys), title, fields: [] },
                    ],
                  };
                })
              }
            >
              + Add section
            </button>
          )}
        </div>
      )}

      {tab === "preview" && (
        <div>
          <p className="mb-3 text-text-muted">
            How the note will look. Try the answers: conditional fields appear
            and disappear as they would on a note.
          </p>
          <TemplateForm
            sections={draft.sections}
            values={preview}
            columns={previewColumns}
            onChange={(v) => setPreview((p) => ({ ...p, [v.key]: v }))}
            onColumnChange={(c, t) =>
              setPreviewColumns((p) => ({ ...p, [c]: t }))
            }
          />
        </div>
      )}

      {tab === "versions" && (
        <div className="list-wrap">
          {versions.isLoading && (
            <p className="p-5 text-text-muted">Loading…</p>
          )}
          {versions.data && (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Version</th>
                  <th>Published</th>
                  <th>By</th>
                  <th>What changed</th>
                  <th>Notes using it</th>
                </tr>
              </thead>
              <tbody>
                {versions.data.map((v) => (
                  <tr key={v.id}>
                    <td data-label="Version">v{v.versionNumber}</td>
                    <td data-label="Published">
                      {new Date(v.createdAt).toLocaleString("en-US")}
                    </td>
                    <td data-label="By">{v.createdByName ?? "System"}</td>
                    <td data-label="What changed">{v.changeSummary ?? "—"}</td>
                    <td data-label="Notes using it">{v.notesUsing}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}

function SectionEditor({
  section: s,
  index,
  count,
  readOnly,
  allFields,
  allKeys,
  onChange,
  onMove,
  onRemove,
}: {
  section: TemplateSection;
  index: number;
  count: number;
  readOnly: boolean;
  allFields: TemplateField[];
  allKeys: ReadonlySet<string>;
  onChange: (s: TemplateSection) => void;
  onMove: (by: number) => void;
  onRemove: () => void;
}) {
  const setField = (i: number, f: TemplateField) =>
    onChange({ ...s, fields: s.fields.map((x, j) => (j === i ? f : x)) });
  const moveField = (i: number, by: number) => {
    const next = [...s.fields];
    const [f] = next.splice(i, 1);
    next.splice(i + by, 0, f);
    onChange({ ...s, fields: next });
  };
  return (
    <section
      aria-label={`Section ${s.title}`}
      className="space-y-3 rounded-lg border border-border bg-white p-4"
    >
      <div className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
        <label className="block">
          <span className="font-bold text-[#333]">Section title</span>
          <input
            className="field-input mt-1"
            readOnly={readOnly}
            value={s.title}
            onChange={(e) => onChange({ ...s, title: e.target.value })}
          />
        </label>
        <label className="block">
          <span className="font-bold text-[#333]">Clinical component</span>
          <select
            className="field-input mt-1"
            disabled={readOnly}
            value={s.component ?? ""}
            onChange={(e) =>
              onChange({ ...s, component: e.target.value || null })
            }
          >
            <option value="">None</option>
            {Object.entries(ComponentLabels).map(([k, l]) => (
              <option key={k} value={k}>
                {l}
              </option>
            ))}
          </select>
        </label>
        {!readOnly && (
          <div className="flex items-end gap-1">
            <button
              type="button"
              className="btn-refresh"
              aria-label="Move section up"
              disabled={index === 0}
              onClick={() => onMove(-1)}
            >
              ↑
            </button>
            <button
              type="button"
              className="btn-refresh"
              aria-label="Move section down"
              disabled={index === count - 1}
              onClick={() => onMove(1)}
            >
              ↓
            </button>
            <button
              type="button"
              className="btn-refresh"
              aria-label={`Remove section ${s.title}`}
              onClick={onRemove}
            >
              ×
            </button>
          </div>
        )}
      </div>
      <label className="block">
        <span className="font-bold text-[#333]">Help text</span>
        <input
          className="field-input mt-1"
          readOnly={readOnly}
          value={s.helpText ?? ""}
          onChange={(e) => onChange({ ...s, helpText: e.target.value || null })}
        />
      </label>

      <ol className="space-y-2">
        {s.fields.map((f, i) => (
          <FieldEditor
            key={i}
            field={f}
            index={i}
            count={s.fields.length}
            readOnly={readOnly}
            otherFields={allFields.filter((x) => x.key !== f.key)}
            onChange={(next) => setField(i, next)}
            onMove={(by) => moveField(i, by)}
            onRemove={() =>
              onChange({ ...s, fields: s.fields.filter((_, j) => j !== i) })
            }
          />
        ))}
      </ol>
      {!readOnly && (
        <button
          type="button"
          className="btn-refresh"
          onClick={() => {
            const label = "New field";
            onChange({
              ...s,
              fields: [
                ...s.fields,
                {
                  key: toKey(label, allKeys),
                  label,
                  fieldType: FieldType.ShortText,
                  isRequired: false,
                },
              ],
            });
          }}
        >
          + Add field
        </button>
      )}
    </section>
  );
}

function FieldEditor({
  field: f,
  index,
  count,
  readOnly,
  otherFields,
  onChange,
  onMove,
  onRemove,
}: {
  field: TemplateField;
  index: number;
  count: number;
  readOnly: boolean;
  otherFields: TemplateField[];
  onChange: (f: TemplateField) => void;
  onMove: (by: number) => void;
  onRemove: () => void;
}) {
  const [open, setOpen] = useState(false);
  const set = (patch: Partial<TemplateField>) => onChange({ ...f, ...patch });
  const v = f.validation ?? {};
  const numeric =
    f.fieldType === FieldType.Number ||
    f.fieldType === FieldType.ClinicalMeasurement;
  return (
    <li className="rounded-md border border-border">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 p-2">
        <button
          type="button"
          className="table-link min-h-11 text-left font-bold"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          {open ? "▾" : "▸"} {f.label || "(no label)"}
        </button>
        <span className="text-text-muted">
          {FieldTypeLabels[f.fieldType]}
          {f.isRequired ? " · required" : ""}
          {f.condition ? " · conditional" : ""}
          {f.noteColumn ? ` · ${NoteColumnLabels[f.noteColumn]}` : ""}
        </span>
        {!readOnly && (
          <span className="ml-auto flex gap-1">
            <button
              type="button"
              className="btn-refresh"
              aria-label={`Move ${f.label} up`}
              disabled={index === 0}
              onClick={() => onMove(-1)}
            >
              ↑
            </button>
            <button
              type="button"
              className="btn-refresh"
              aria-label={`Move ${f.label} down`}
              disabled={index === count - 1}
              onClick={() => onMove(1)}
            >
              ↓
            </button>
            <button
              type="button"
              className="btn-refresh"
              aria-label={`Remove ${f.label}`}
              onClick={onRemove}
            >
              ×
            </button>
          </span>
        )}
      </div>
      {open && (
        <fieldset
          disabled={readOnly}
          className="grid gap-3 border-t border-border p-3 md:grid-cols-2"
        >
          <legend className="sr-only">{f.label} settings</legend>
          <label className="block">
            <span className="font-bold text-[#333]">Label</span>
            <input
              className="field-input mt-1"
              value={f.label}
              onChange={(e) => set({ label: e.target.value })}
            />
          </label>
          <label className="block">
            <span className="font-bold text-[#333]">Key</span>
            <span className="ml-2 text-text-muted">
              — stored with each note’s value
            </span>
            <input
              className="field-input mt-1"
              value={f.key}
              onChange={(e) => set({ key: e.target.value })}
            />
          </label>
          <label className="block">
            <span className="font-bold text-[#333]">Type</span>
            <select
              className="field-input mt-1"
              value={f.fieldType}
              onChange={(e) => {
                const fieldType = Number(e.target.value) as FieldType;
                set({
                  fieldType,
                  noteColumn: TEXT_TYPES.has(fieldType) ? f.noteColumn : null,
                  isRequired:
                    fieldType === FieldType.Signature ? false : f.isRequired,
                  scaleMin: fieldType === FieldType.PainScale ? 0 : null,
                  scaleMax: fieldType === FieldType.PainScale ? 10 : null,
                });
              }}
            >
              {Object.entries(FieldTypeLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label className="flex min-h-11 items-center gap-2 self-end">
            <input
              type="checkbox"
              className="h-5 w-5"
              checked={f.isRequired}
              disabled={f.fieldType === FieldType.Signature}
              onChange={(e) => set({ isRequired: e.target.checked })}
            />
            <span className="font-bold text-[#333]">Required to sign</span>
          </label>
          <label className="block md:col-span-2">
            <span className="font-bold text-[#333]">Help text</span>
            <input
              className="field-input mt-1"
              value={f.helpText ?? ""}
              onChange={(e) => set({ helpText: e.target.value || null })}
            />
          </label>
          {(TEXT_TYPES.has(f.fieldType) || numeric) && (
            <label className="block">
              <span className="font-bold text-[#333]">Placeholder</span>
              <input
                className="field-input mt-1"
                value={f.placeholder ?? ""}
                onChange={(e) => set({ placeholder: e.target.value || null })}
              />
            </label>
          )}
          {numeric && (
            <label className="block">
              <span className="font-bold text-[#333]">Unit</span>
              <input
                className="field-input mt-1"
                value={f.unit ?? ""}
                placeholder="e.g. deg, sec, ft"
                onChange={(e) => set({ unit: e.target.value || null })}
              />
            </label>
          )}
          {TEXT_TYPES.has(f.fieldType) && (
            <label className="block">
              <span className="font-bold text-[#333]">Store in the note’s</span>
              <select
                className="field-input mt-1"
                value={f.noteColumn ?? ""}
                onChange={(e) => set({ noteColumn: e.target.value || null })}
              >
                <option value="">Own field (not a S/O/A/P column)</option>
                {Object.entries(NoteColumnLabels).map(([k, l]) => (
                  <option key={k} value={k}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
          )}
          {OPTION_TYPES.has(f.fieldType) && (
            <label className="block md:col-span-2">
              <span className="font-bold text-[#333]">Choices</span>
              <span className="ml-2 text-text-muted">— one per line</span>
              <textarea
                className="field-input mt-1 min-h-[5rem]"
                value={(f.options ?? []).join("\n")}
                onChange={(e) => set({ options: e.target.value.split("\n") })}
              />
            </label>
          )}
          {f.fieldType === FieldType.StructuredTable && (
            <label className="block md:col-span-2">
              <span className="font-bold text-[#333]">Columns</span>
              <span className="ml-2 text-text-muted">— one per line</span>
              <textarea
                className="field-input mt-1 min-h-[5rem]"
                value={(f.columns ?? []).map((c) => c.label).join("\n")}
                onChange={(e) => {
                  const taken = new Set<string>();
                  set({
                    columns: e.target.value.split("\n").map((label) => {
                      const key = toKey(label, taken);
                      taken.add(key);
                      return { key, label };
                    }),
                  });
                }}
              />
            </label>
          )}
          {f.fieldType === FieldType.PainScale && (
            <p className="text-text-muted md:col-span-2">Scored 0–10.</p>
          )}
          {numeric && (
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="font-bold text-[#333]">Minimum</span>
                <input
                  type="number"
                  className="field-input mt-1"
                  value={v.min ?? ""}
                  onChange={(e) =>
                    set({
                      validation: {
                        ...v,
                        min:
                          e.target.value === "" ? null : Number(e.target.value),
                      },
                    })
                  }
                />
              </label>
              <label className="block">
                <span className="font-bold text-[#333]">Maximum</span>
                <input
                  type="number"
                  className="field-input mt-1"
                  value={v.max ?? ""}
                  onChange={(e) =>
                    set({
                      validation: {
                        ...v,
                        max:
                          e.target.value === "" ? null : Number(e.target.value),
                      },
                    })
                  }
                />
              </label>
            </div>
          )}
          {TEXT_TYPES.has(f.fieldType) && (
            <label className="block">
              <span className="font-bold text-[#333]">Maximum length</span>
              <input
                type="number"
                min={1}
                className="field-input mt-1"
                value={v.maxLength ?? ""}
                onChange={(e) =>
                  set({
                    validation: {
                      ...v,
                      maxLength:
                        e.target.value === "" ? null : Number(e.target.value),
                    },
                  })
                }
              />
            </label>
          )}
          {f.fieldType === FieldType.ShortText && (
            <label className="block">
              <span className="font-bold text-[#333]">
                Pattern (regular expression)
              </span>
              <input
                className="field-input mt-1"
                value={v.pattern ?? ""}
                onChange={(e) =>
                  set({ validation: { ...v, pattern: e.target.value || null } })
                }
              />
            </label>
          )}
          <fieldset className="md:col-span-2">
            <legend className="font-bold text-[#333]">Show only when</legend>
            <div className="mt-1 grid gap-3 md:grid-cols-2">
              <select
                aria-label="Depends on field"
                className="field-input"
                value={f.condition?.field ?? ""}
                onChange={(e) =>
                  set({
                    condition: e.target.value
                      ? {
                          field: e.target.value,
                          value: f.condition?.value ?? "",
                        }
                      : null,
                  })
                }
              >
                <option value="">Always shown</option>
                {otherFields.map((o) => (
                  <option key={o.key} value={o.key}>
                    {o.label}
                  </option>
                ))}
              </select>
              {f.condition && (
                <input
                  aria-label="Equals"
                  className="field-input"
                  placeholder="equals (e.g. Yes, or true for a checkbox)"
                  value={f.condition.value ?? ""}
                  onChange={(e) =>
                    set({
                      condition: { ...f.condition!, value: e.target.value },
                    })
                  }
                />
              )}
            </div>
          </fieldset>
        </fieldset>
      )}
    </li>
  );
}

import { useState } from "react";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { RowMenu } from "../../../components/RowMenu";
import { useToast } from "../../../components/Toast";
import { AdminPageHeader } from "../../admin/AdminPageHeader";
import { useAuth } from "../../auth/AuthProvider";
import { RoleSets, canAccess } from "../../auth/permissions";
import { NoteTypeLabels, TEMPLATE_NOTE_TYPES } from "../../workflow/types";
import {
  copyTemplate,
  fetchTemplates,
  setTemplateActive,
  setTemplateFavorite,
} from "../api";
import { SpecialtyLabels, type DocTemplate } from "../types";

/** Documentation templates: system templates and the clinic's own, with
 * filters, favorites, activation and copying. */
export function TemplatesPage() {
  const { user } = useAuth();
  const canManage = canAccess(user, RoleSets.OrganizationAdministration);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [noteType, setNoteType] = useState<string>("");
  const [specialty, setSpecialty] = useState<string>("");
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);

  const filter = {
    noteType: noteType === "" ? null : Number(noteType),
    specialty: specialty === "" ? null : Number(specialty),
    includeInactive,
    search,
  };
  const list = useQuery({
    queryKey: ["templates", filter],
    queryFn: () => fetchTemplates(filter),
    placeholderData: keepPreviousData,
  });
  const refresh = () =>
    void queryClient.invalidateQueries({ queryKey: ["templates"] });

  const favorite = useMutation({
    mutationFn: (t: DocTemplate) => setTemplateFavorite(t.id, !t.isFavorite),
    onSuccess: refresh,
    onError: (e: Error) => showToast(e.message),
  });
  const toggleActive = useMutation({
    mutationFn: (t: DocTemplate) => setTemplateActive(t.id, !t.isActive),
    onSuccess: (t) => {
      showToast(`${t.name} ${t.isActive ? "activated" : "deactivated"}.`);
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const copy = useMutation({
    mutationFn: (t: DocTemplate) => copyTemplate(t.id),
    onSuccess: (d) => {
      showToast(`Copied as ${d.template.name}.`);
      refresh();
      navigate(`/admin/templates/${d.template.id}`);
    },
    onError: (e: Error) => showToast(e.message),
  });

  return (
    <div>
      <AdminPageHeader
        title="Documentation Templates"
        actions={
          canManage && (
            <Link to="/admin/templates/new" className="btn-primary">
              + New template
            </Link>
          )
        }
      />

      <div className="list-toolbar">
        <label className="toolbar-label">
          Note type
          <select
            className="toolbar-select"
            value={noteType}
            onChange={(e) => setNoteType(e.target.value)}
          >
            <option value="">All</option>
            {TEMPLATE_NOTE_TYPES.map((t) => (
              <option key={t} value={t}>
                {NoteTypeLabels[t]}
              </option>
            ))}
          </select>
        </label>
        <label className="toolbar-label">
          Specialty
          <select
            className="toolbar-select"
            value={specialty}
            onChange={(e) => setSpecialty(e.target.value)}
          >
            <option value="">All</option>
            {Object.entries(SpecialtyLabels).map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
        </label>
        <label className="toolbar-label">
          Search
          <input
            type="search"
            className="toolbar-select"
            value={search}
            placeholder="Name or description"
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <label className="toolbar-label">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(e) => setIncludeInactive(e.target.checked)}
          />
          Show inactive
        </label>
      </div>

      <div className="list-wrap">
        {list.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {list.isError && (
          <p className="alert-error m-5">{list.error.message}</p>
        )}
        {list.data && list.data.length === 0 && (
          <p className="p-5 text-text-muted">No templates match.</p>
        )}
        {!!list.data?.length && (
          <table className="data-table">
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Name</th>
                <th>Note type</th>
                <th>Specialty</th>
                <th>Source</th>
                <th>Version</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {list.data.map((t) => (
                <tr key={t.id} className={t.isActive ? "" : "opacity-60"}>
                  <td className="cell-menu">
                    <RowMenu
                      label={`Actions for ${t.name}`}
                      items={[
                        {
                          label: t.isSystem || !canManage ? "View" : "Edit",
                          to: `/admin/templates/${t.id}`,
                        },
                        {
                          label: t.isFavorite
                            ? "Remove from favorites"
                            : "Add to favorites",
                          onSelect: () => favorite.mutate(t),
                        },
                        ...(canManage
                          ? [{ label: "Copy", onSelect: () => copy.mutate(t) }]
                          : []),
                        ...(canManage && !t.isSystem
                          ? [
                              {
                                label: t.isActive ? "Deactivate" : "Activate",
                                onSelect: () => toggleActive.mutate(t),
                              },
                            ]
                          : []),
                      ]}
                    />
                  </td>
                  <td data-label="Name">
                    <Link
                      to={`/admin/templates/${t.id}`}
                      className="table-link"
                    >
                      {t.isFavorite && (
                        <>
                          <span className="sr-only">Favorite: </span>
                          <span
                            aria-hidden="true"
                            title="Favorite"
                            className="mr-1 text-warning"
                          >
                            ★
                          </span>
                        </>
                      )}
                      {t.name}
                    </Link>
                  </td>
                  <td data-label="Note type">
                    {NoteTypeLabels[t.noteType] ?? t.noteType}
                  </td>
                  <td data-label="Specialty">{SpecialtyLabels[t.specialty]}</td>
                  <td data-label="Source">
                    {t.isSystem ? "System" : "Clinic"}
                  </td>
                  <td data-label="Version">v{t.currentVersionNumber}</td>
                  <td data-label="Status">
                    {t.isActive ? "Active" : "Inactive"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

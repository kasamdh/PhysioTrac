import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RowMenu } from "../../../components/RowMenu";
import { SegmentedButtons } from "../../../components/SegmentedButtons";
import { useToast } from "../../../components/Toast";
import { useAuth } from "../../auth/AuthProvider";
import { UserRole, UserRoleLabels } from "../../auth/types";
import { AdminPageHeader } from "../AdminPageHeader";
import { fetchUsers, inviteUser } from "../api";
import { EditUserDialog } from "../EditUserDialog";
import { UserStatus, UserStatusLabels, type InviteUserInput, type StaffUser } from "../types";

// Staff roles an organization admin can assign. SuperAdmin is platform-only
// and Patient accounts come from the patient portal, not this page.
const ASSIGNABLE_ROLES: UserRole[] = [
  UserRole.Admin,
  UserRole.Director,
  UserRole.Therapist,
  UserRole.Assistant,
  UserRole.Scheduler,
  UserRole.Biller,
  UserRole.Compliance,
];

export function UsersAdminPage() {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { user: me } = useAuth();
  const [adding, setAdding] = useState(false);
  const [invited, setInvited] = useState<{ name: string; url: string } | null>(null);
  const [filter, setFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "invited" | "suspended" | "deleted">("all");
  const [editing, setEditing] = useState<StaffUser | null>(null);

  const users = useQuery({ queryKey: ["admin", "users"], queryFn: fetchUsers });
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["admin", "users"] });

  const term = filter.trim().toLowerCase();
  const rows = (users.data ?? [])
    .filter((u) => !term || `${u.firstName} ${u.lastName} ${u.userName} ${u.email ?? ""}`.toLowerCase().includes(term))
    .filter(
      (u) =>
        statusFilter === "all" ||
        (statusFilter === "active" && u.status === UserStatus.Active) ||
        (statusFilter === "invited" && u.status === UserStatus.Inactive) ||
        (statusFilter === "suspended" && u.status === UserStatus.Suspended) ||
        (statusFilter === "deleted" && u.status === UserStatus.Deleted),
    )
    .sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`));

  return (
    <div>
      <AdminPageHeader
        title="Users"
        actions={
          <>
            <button
              type="button"
              className="btn-primary"
              onClick={() => {
                setInvited(null);
                setAdding(true);
              }}
            >
              + New user
            </button>
          </>
        }
      />

      <div className="list-toolbar">
        <SegmentedButtons
          label="Account status"
          value={statusFilter}
          onChange={setStatusFilter}
          options={[
            { value: "all", label: "All Users" },
            { value: "active", label: "Active Users" },
            { value: "invited", label: "Invited" },
            { value: "suspended", label: "Suspended Users" },
            { value: "deleted", label: "Deleted Users" },
          ]}
        />
        <input
          type="search"
          aria-label="Search users"
          className="toolbar-select w-64"
          placeholder="Search User"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
        <button type="button" className="btn-refresh ml-auto" disabled={users.isFetching} onClick={() => void users.refetch()}>
          {users.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      {invited && (
        <div className="card mb-5 border-success bg-success-light/40">
          <p className="font-semibold text-text">Account created for {invited.name}.</p>
          <p className="mt-1 text-sm text-text-muted">
            Email isn't connected yet, so send them this one-time link to set their password:
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <code className="min-w-0 flex-1 break-all rounded bg-white px-2 py-1.5 text-xs">{invited.url}</code>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                void navigator.clipboard?.writeText(invited.url);
                showToast("Activation link copied.");
              }}
            >
              Copy link
            </button>
            <button type="button" className="btn-secondary" onClick={() => setInvited(null)}>
              Dismiss
            </button>
          </div>
        </div>
      )}

      {editing && (
        <EditUserDialog
          user={editing}
          isSelf={editing.id === me?.id}
          assignableRoles={ASSIGNABLE_ROLES}
          onClose={() => setEditing(null)}
          onSaved={(u, passwordReset) => {
            setEditing(null);
            showToast(`${u.firstName} ${u.lastName} saved.${passwordReset ? " Their password was reset." : ""}`);
            refresh();
          }}
        />
      )}

      {adding && (
        <NewUserForm
          onClose={() => setAdding(false)}
          onCreated={(u, url) => {
            setAdding(false);
            setInvited({ name: `${u.firstName} ${u.lastName}`, url });
            refresh();
          }}
        />
      )}

      <div className="list-wrap">
        {users.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {users.isError && <p className="alert-error m-5">{users.error.message}</p>}
        {users.data && (
          <table className="data-table">
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Name</th>
                <th>User ID</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((u) => {
                const isMe = u.id === me?.id;
                const inactive = u.status === UserStatus.Suspended || u.status === UserStatus.Deleted;
                return (
                  <tr key={u.id} className={inactive ? "opacity-60" : ""}>
                    <td className="cell-menu">
                      <RowMenu
                        label={`Actions for ${u.firstName} ${u.lastName}`}
                        items={[{ label: "Edit User", onSelect: () => setEditing(u) }]}
                      />
                    </td>
                    <td data-label="Name">
                      <button type="button" className="table-link text-left" onClick={() => setEditing(u)}>
                        {u.firstName} {u.lastName}
                      </button>
                      {isMe && <span className="ml-1 text-xs text-text-muted">(you)</span>}
                    </td>
                    <td data-label="User ID">{u.userName}</td>
                    <td data-label="Email" className="text-text-muted">{u.email}</td>
                    <td data-label="Role">{UserRoleLabels[u.role]}</td>
                    <td data-label="Status">{u.status === UserStatus.Inactive ? "Invited" : UserStatusLabels[u.status]}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function NewUserForm({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (user: StaffUser, activationUrl: string) => void;
}) {
  const [form, setForm] = useState<InviteUserInput>({ firstName: "", lastName: "", email: "", role: UserRole.Therapist });
  const create = useMutation({
    mutationFn: () => inviteUser({ ...form, firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim() }),
    onSuccess: (r) => onCreated(r.user, r.activationUrl),
  });
  const valid = form.firstName.trim() && form.lastName.trim() && /\S+@\S+\.\S+/.test(form.email);

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    create.mutate();
  };

  return (
    <form onSubmit={onSubmit} className="card mb-5">
      <h2 className="mb-1 text-lg font-semibold text-text">New user</h2>
      <p className="mb-4 text-sm text-text-muted">
        The account is created without a password; you'll get a one-time link for them to set one.
      </p>
      {create.isError && <p className="alert-error mb-4">{create.error.message}</p>}
      <div className="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2 lg:grid-cols-4">
        <label className="field-label">
          First name *
          <input
            className="field-input mt-1"
            autoFocus
            value={form.firstName}
            onChange={(e) => setForm({ ...form, firstName: e.target.value })}
          />
        </label>
        <label className="field-label">
          Last name *
          <input className="field-input mt-1" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
        </label>
        <label className="field-label">
          Email *
          <input
            type="email"
            className="field-input mt-1"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
          />
        </label>
        <label className="field-label">
          Role *
          <select
            className="field-input mt-1"
            value={form.role}
            onChange={(e) => setForm({ ...form, role: Number(e.target.value) as UserRole })}
          >
            {ASSIGNABLE_ROLES.map((r) => (
              <option key={r} value={r}>
                {UserRoleLabels[r]}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="mt-5 flex justify-end gap-2">
        <button type="button" className="btn-secondary" onClick={onClose}>
          Cancel
        </button>
        <button type="submit" className="btn-primary" disabled={!valid || create.isPending}>
          {create.isPending ? "Creating…" : "Create user"}
        </button>
      </div>
    </form>
  );
}

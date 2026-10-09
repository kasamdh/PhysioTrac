import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { SegmentedButtons } from "../../components/SegmentedButtons";
import { UserRole, UserRoleLabels } from "../auth/types";
import { updateUser } from "./api";
import { FormRow } from "./FormRow";
import { passwordProblems } from "./PasswordField";
import { UserStatus, type StaffUser } from "./types";

type StatusChoice = "active" | "suspended" | "deleted";
const statusOf: Record<StatusChoice, UserStatus> = {
  active: UserStatus.Active,
  suspended: UserStatus.Suspended,
  deleted: UserStatus.Deleted,
};
const choiceOf = (s: UserStatus): StatusChoice | null =>
  s === UserStatus.Active ? "active" : s === UserStatus.Suspended ? "suspended" : s === UserStatus.Deleted ? "deleted" : null;

/** Administration › Users › Edit User. */
export function EditUserDialog({
  user,
  isSelf,
  assignableRoles,
  onClose,
  onSaved,
}: {
  user: StaffUser;
  /** Editing your own account: status and access level are locked. */
  isSelf: boolean;
  assignableRoles: UserRole[];
  onClose: () => void;
  onSaved: (user: StaffUser, passwordReset: boolean) => void;
}) {
  const titleId = useId();
  const firstFieldRef = useRef<HTMLInputElement>(null);
  const [userName, setUserName] = useState(user.userName);
  const [firstName, setFirstName] = useState(user.firstName);
  const [lastName, setLastName] = useState(user.lastName);
  const [email, setEmail] = useState(user.email ?? "");
  const [role, setRole] = useState<UserRole>(user.role);
  // null = a pending invite (Inactive), which isn't one of the three choices.
  const [status, setStatus] = useState<StatusChoice | null>(choiceOf(user.status));
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => {
    firstFieldRef.current?.focus();
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  const save = useMutation({
    mutationFn: () =>
      updateUser(user.id, {
        userName: userName.trim(),
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim() || null,
        role,
        status: status ? statusOf[status] : user.status,
        newPassword: password || null,
      }),
    onSuccess: (saved) => onSaved(saved, !!password),
  });

  const problems = password ? passwordProblems(password) : [];
  // Loose on purpose: an existing address must never block saving other fields.
  const emailInvalid = !!email.trim() && !/^[^\s@]+@[^\s@]+$/.test(email.trim());
  const valid = userName.trim() && firstName.trim() && lastName.trim() && !emailInvalid && problems.length === 0;
  const dirty =
    userName.trim() !== user.userName ||
    firstName.trim() !== user.firstName ||
    lastName.trim() !== user.lastName ||
    (email.trim() || null) !== (user.email ?? null) ||
    role !== user.role ||
    (status !== null && statusOf[status] !== user.status) ||
    !!password;

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (valid && dirty) save.mutate();
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onSubmit={onSubmit}
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl"
        noValidate
      >
        <h2 id={titleId} className="border-b border-border px-6 py-4 text-3xl text-[#333]">
          Edit User
        </h2>

        <div className="space-y-4 overflow-y-auto px-6 py-5">
          {save.isError && <p className="alert-error">{save.error.message}</p>}

          <FormRow label="User ID" htmlFor="edit-user-id">
            <input
              id="edit-user-id"
              ref={firstFieldRef}
              className="field-input"
              autoComplete="off"
              value={userName}
              onChange={(e) => setUserName(e.target.value)}
            />
          </FormRow>

          <FormRow label="User Name" htmlFor="edit-first-name">
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <input
                id="edit-first-name"
                aria-label="First name"
                placeholder="First name"
                className="field-input"
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
              />
              <input
                aria-label="Last name"
                placeholder="Last name"
                className="field-input"
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
              />
            </div>
          </FormRow>

          <FormRow label="New Password" htmlFor="edit-password">
            <div className="flex items-center gap-3">
              <div className="relative flex-1">
                <input
                  id="edit-password"
                  type={showPassword ? "text" : "password"}
                  autoComplete="new-password"
                  className="field-input pr-11"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  aria-describedby="edit-password-help"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Hide new password" : "Show new password"}
                  aria-pressed={showPassword}
                  className="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-slate-500 hover:text-primary"
                >
                  <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                    <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
                    <circle cx="12" cy="12" r="3" />
                    {showPassword && <path d="M3 3l18 18" />}
                  </svg>
                </button>
              </div>
              <span
                title="Leave blank to keep the current password. Setting one signs the user out everywhere."
                className="shrink-0 text-[#2a6eb0]"
                aria-hidden="true"
              >
                <svg viewBox="0 0 20 20" className="h-6 w-6">
                  <circle cx="10" cy="10" r="10" fill="currentColor" />
                  <rect x="9" y="8.5" width="2" height="6.5" rx="1" fill="#fff" />
                  <circle cx="10" cy="5.6" r="1.25" fill="#fff" />
                </svg>
              </span>
            </div>
            <p id="edit-password-help" className={`mt-1 ${problems.length ? "text-danger" : "text-text-muted"}`}>
              {problems.length
                ? `Needs ${problems.join(", ")}.`
                : "Leave blank to keep the current password. Setting one signs the user out everywhere."}
            </p>
          </FormRow>

          <FormRow label="Status">
            {isSelf ? (
              <p className="py-2 text-text-muted">You can't change your own status.</p>
            ) : (
              <>
                <SegmentedButtons
                  label="Status"
                  value={(status ?? "") as StatusChoice}
                  onChange={setStatus}
                  options={[
                    { value: "active", label: "Active" },
                    { value: "suspended", label: "Suspended" },
                    { value: "deleted", label: "Deleted" },
                  ]}
                />
                {status === null && (
                  <p className="mt-1 text-text-muted">
                    Invited — they haven't set a password yet. Setting one here activates the account.
                  </p>
                )}
                {status !== null && status !== "active" && statusOf[status] !== user.status && (
                  <p className="mt-1 text-danger">They'll be signed out everywhere when you save.</p>
                )}
              </>
            )}
          </FormRow>

          <FormRow label="Access Level" htmlFor="edit-role">
            <select
              id="edit-role"
              className="field-input"
              value={role}
              disabled={isSelf}
              onChange={(e) => setRole(Number(e.target.value) as UserRole)}
            >
              {!assignableRoles.includes(user.role) && <option value={user.role}>{UserRoleLabels[user.role]}</option>}
              {assignableRoles.map((r) => (
                <option key={r} value={r}>
                  {UserRoleLabels[r]}
                </option>
              ))}
            </select>
            {isSelf && <p className="mt-1 text-text-muted">You can't change your own access level.</p>}
          </FormRow>

          <FormRow label="E-Mail Address" htmlFor="edit-email">
            <input
              id="edit-email"
              type="email"
              className="field-input"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
            {emailInvalid && <p className="mt-1 text-danger">Enter a valid email address.</p>}
          </FormRow>
        </div>

        <div className="flex justify-end gap-3 border-t border-border px-6 py-4">
          <button type="button" className="btn-refresh" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn-primary" disabled={!valid || !dirty || save.isPending}>
            {save.isPending ? "Saving…" : "Save and Close"}
          </button>
        </div>
      </form>
    </div>
  );
}

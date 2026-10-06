import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { ApiError } from "../../../lib/apiClient";
import { AdminPageHeader } from "../AdminPageHeader";
import { changePassword } from "../api";
import { PasswordField, passwordProblems } from "../PasswordField";

export function ChangePasswordPage() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [done, setDone] = useState(false);

  const problems = next ? passwordProblems(next) : [];
  const mismatch = confirm.length > 0 && confirm !== next;
  const canSubmit = current && next && confirm && problems.length === 0 && !mismatch;

  const save = useMutation({
    mutationFn: () => changePassword(current, next),
    onSuccess: () => {
      setDone(true);
      setCurrent("");
      setNext("");
      setConfirm("");
    },
  });
  const serverErrors =
    save.error instanceof ApiError ? ((save.error.payload as { errors?: string[] } | undefined)?.errors ?? []) : [];

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setDone(false);
    save.mutate();
  };

  return (
    <div className="mx-auto max-w-md">
      <AdminPageHeader title="Change Password" />
      <form onSubmit={onSubmit} className="card space-y-4">
        {done && <p className="rounded-md bg-success-light px-3 py-2 text-sm text-success">Your password was changed.</p>}
        {save.isError && (
          <div className="alert-error">
            {save.error.message}
            {serverErrors.length > 0 && (
              <ul className="mt-1 list-disc pl-5">
                {serverErrors.map((e) => (
                  <li key={e}>{e}</li>
                ))}
              </ul>
            )}
          </div>
        )}
        <PasswordField
          id="current-password"
          label="Current password"
          autoComplete="current-password"
          value={current}
          onChange={(e) => setCurrent(e.target.value)}
        />
        <div>
          <PasswordField
            id="new-password"
            label="New password"
            autoComplete="new-password"
            value={next}
            onChange={(e) => setNext(e.target.value)}
          />
          <p className={`mt-1 text-xs ${problems.length ? "text-danger" : "text-text-muted"}`}>
            {problems.length
              ? `Needs ${problems.join(", ")}.`
              : "10+ characters with an uppercase letter, a lowercase letter, a number and a symbol."}
          </p>
        </div>
        <div>
          <PasswordField
            id="confirm-password"
            label="Confirm new password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
          />
          {mismatch && <p className="mt-1 text-xs text-danger">Passwords don't match.</p>}
        </div>
        <button type="submit" className="btn-primary w-full" disabled={!canSubmit || save.isPending}>
          {save.isPending ? "Saving…" : "Change password"}
        </button>
      </form>
    </div>
  );
}

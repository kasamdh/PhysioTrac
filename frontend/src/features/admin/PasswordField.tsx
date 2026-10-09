import { useState, type InputHTMLAttributes } from "react";

/** Password input with a show/hide eye toggle. */
export function PasswordField({ label, id, ...props }: { label: string; id: string } & InputHTMLAttributes<HTMLInputElement>) {
  const [visible, setVisible] = useState(false);
  return (
    <div>
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <div className="relative">
        <input id={id} type={visible ? "text" : "password"} className="field-input pr-10" {...props} />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}
          aria-pressed={visible}
          className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-slate-500 hover:text-primary"
        >
          <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
            <circle cx="12" cy="12" r="3" />
            {visible && <path d="M3 3l18 18" />}
          </svg>
        </button>
      </div>
    </div>
  );
}

/** Mirrors the API's Identity options (Program.cs): 10+ characters, upper,
 * lower, digit and symbol. Invitation activation asks for 12+ (AuthController),
 * hence `minLength`. Returns the unmet rules. */
export function passwordProblems(password: string, minLength = 10): string[] {
  const problems: string[] = [];
  if (password.length < minLength) problems.push(`at least ${minLength} characters`);
  if (!/[A-Z]/.test(password)) problems.push("an uppercase letter");
  if (!/[a-z]/.test(password)) problems.push("a lowercase letter");
  if (!/\d/.test(password)) problems.push("a number");
  if (!/[^A-Za-z0-9]/.test(password)) problems.push("a symbol");
  return problems;
}

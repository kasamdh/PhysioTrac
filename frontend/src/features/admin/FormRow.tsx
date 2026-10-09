import type { ReactNode } from "react";

/** Dialog form row: label on the left (right-aligned), field on the right;
 * stacked on phones. `missing` marks a required field that is still empty:
 * dark-red label plus a "Required" line under the field. */
export function FormRow({
  label,
  htmlFor,
  missing = false,
  children,
}: {
  label: string;
  htmlFor?: string;
  missing?: boolean;
  children: ReactNode;
}) {
  const labelClass = `font-bold sm:pt-2 sm:text-right ${missing ? "text-[#7a1c1c]" : "text-[#333]"}`;
  return (
    <div className="grid grid-cols-1 gap-1 sm:grid-cols-[10rem_1fr] sm:items-start sm:gap-5">
      {htmlFor ? (
        <label htmlFor={htmlFor} className={labelClass}>
          {label}
        </label>
      ) : (
        <span className={labelClass}>{label}</span>
      )}
      <div className={`min-w-0 ${missing ? "form-row-missing" : ""}`}>
        {children}
        {missing && <p className="mt-1 text-[#7a1c1c]">Required</p>}
      </div>
    </div>
  );
}

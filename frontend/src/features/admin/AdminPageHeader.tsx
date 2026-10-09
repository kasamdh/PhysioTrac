import type { ReactNode } from "react";
import { Link } from "react-router-dom";

/** Title row for Administration sub-pages (and pages built the same way): a
 * back link -- the Administration hub unless `back` says otherwise, none when
 * null -- the page title, and optional actions on the right. */
export function AdminPageHeader({
  title,
  actions,
  back = { to: "/admin", label: "Administration" },
}: {
  title: string;
  actions?: ReactNode;
  back?: { to: string; label: string } | null;
}) {
  return (
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
      <div>
        {back && (
          <Link to={back.to} className="text-sm text-primary hover:underline">
            ‹ {back.label}
          </Link>
        )}
        <h1 className="mt-1 text-2xl font-bold text-[#1565b8]">{title}</h1>
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

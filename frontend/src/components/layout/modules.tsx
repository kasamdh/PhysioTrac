import type { ReactNode } from "react";
import { RoleSets } from "../../features/auth/permissions";
import type { UserRole } from "../../features/auth/types";

// One list drives both the header's module tabs and the Home page tiles.
// `allowed` undefined = every signed-in user (Home).
export interface AppModule {
  to: string;
  label: string;
  end: boolean;
  allowed?: ReadonlySet<UserRole>;
  /** Shown on the Home page as a tile; false keeps it header-only. */
  tile: boolean;
  /** Tailwind gradient classes for the tile art. */
  tileGradient: string;
  icon: ReactNode;
}

const stroke = {
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.6,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
};

export const appModules: AppModule[] = [
  {
    to: "/",
    label: "Home",
    end: true,
    tile: false,
    tileGradient: "",
    icon: null,
  },
  {
    to: "/patients",
    label: "Patients",
    end: false,
    allowed: RoleSets.Clinical,
    tile: true,
    tileGradient: "from-sky-300 via-sky-500 to-blue-700",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <circle cx="9" cy="8" r="3.2" />
        <path d="M3.5 19c.6-3.2 2.8-5 5.5-5s4.9 1.8 5.5 5" />
        <circle cx="17" cy="9" r="2.4" />
        <path d="M15.5 14.2c2.3-.3 4.3 1.1 5 4.3" />
      </svg>
    ),
  },
  {
    to: "/schedule",
    label: "Schedule",
    end: false,
    allowed: RoleSets.Scheduling,
    tile: true,
    tileGradient: "from-fuchsia-400 via-violet-500 to-teal-400",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <rect x="3.5" y="5" width="17" height="15" rx="2" />
        <path d="M3.5 9.5h17M8 3v4M16 3v4" />
        <path d="M8 13.5h2M12 13.5h2M16 13.5h.5M8 16.5h2M12 16.5h2" />
      </svg>
    ),
  },
  {
    to: "/schedule/hours",
    label: "Provider Hours",
    end: false,
    allowed: RoleSets.Scheduling,
    tile: true,
    tileGradient: "from-amber-200 via-amber-400 to-yellow-600",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <circle cx="12" cy="12" r="8.5" />
        <path d="M12 7v5l3.5 2" />
      </svg>
    ),
  },
  {
    to: "/providers",
    label: "Providers",
    end: false,
    allowed: RoleSets.Clinical,
    tile: true,
    tileGradient: "from-blue-500 via-blue-700 to-indigo-950",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <path d="M7 4v5a5 5 0 0 0 10 0V4" />
        <path d="M12 14v2.5a3.5 3.5 0 0 0 7 0v-1" />
        <circle cx="19" cy="13.5" r="2" />
      </svg>
    ),
  },
  {
    to: "/billing",
    label: "Billing",
    end: false,
    allowed: RoleSets.Billing,
    tile: true,
    tileGradient: "from-indigo-400 via-indigo-700 to-slate-900",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <path d="M6 3.5h12v17l-2-1.3-2 1.3-2-1.3-2 1.3-2-1.3-2 1.3z" />
        <path d="M9 8h6M9 11.5h6M9 15h3.5" />
      </svg>
    ),
  },
  {
    to: "/workflow",
    label: "Workflow",
    end: false,
    allowed: RoleSets.Scheduling,
    tile: true,
    tileGradient: "from-cyan-300 via-sky-600 to-blue-900",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <rect x="5" y="4" width="14" height="17" rx="2" />
        <path d="M9 4.5V3h6v1.5" />
        <path d="M8.5 10.5l1.5 1.5 3-3M8.5 16l1.5 1.5 3-3M15 11h1.5M15 16.5h1.5" />
      </svg>
    ),
  },
  {
    // Not a tile or tab: opened from Workflow / a patient's documentation.
    // Listed so the header shows "Clinical Charting" as the page name.
    to: "/chart",
    label: "Clinical Charting",
    end: false,
    allowed: RoleSets.Clinical,
    tile: false,
    tileGradient: "",
    icon: null,
  },
  {
    to: "/admin",
    label: "Administration",
    end: false,
    tile: true,
    tileGradient: "from-blue-400 via-blue-700 to-blue-950",
    icon: (
      <svg viewBox="0 0 24 24" {...stroke}>
        <circle cx="12" cy="12" r="3" />
        <path d="M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1" />
      </svg>
    ),
  },
];

/** Title shown under the welcome line: the most specific module whose path
 * matches, so /schedule/hours reads "Provider Hours", not "Schedule". */
export function moduleTitle(pathname: string): string {
  const match = appModules
    .filter((m) => (m.end ? pathname === m.to : pathname === m.to || pathname.startsWith(`${m.to}/`)))
    .sort((a, b) => b.to.length - a.to.length)[0];
  return match?.label ?? "";
}

import { useCallback, useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../../features/auth/AuthProvider";
import { canAccess } from "../../features/auth/permissions";
import { useIdleTimeout } from "../../hooks/useIdleTimeout";
import { IdleTimeoutWarning } from "../IdleTimeoutWarning";
import { HeaderLogo, LegalFooter } from "../brand/Brand";
import { appModules, moduleTitle } from "./modules";
import { HelpDialog } from "../../features/help/HelpDialog";
import { recordPageView } from "../../features/logs/api";
import { helpKeyFor } from "../../features/help/helpContent";

// Header tabs: every module except sub-pages reached from inside another
// one (Provider Hours lives under Schedule) and Administration / Workflow,
// which open from their Home tiles so the tab row stays short enough for the
// welcome text (see the 1600px breakpoint below).
const headerModules = appModules.filter((m) => !["/schedule/hours", "/admin", "/workflow", "/chart"].includes(m.to));

function tabClass({ isActive }: { isActive: boolean }) {
  return [
    "whitespace-nowrap border-b-[3px] px-1 py-1.5 text-xl transition",
    isActive ? "border-white text-white" : "border-transparent text-white/90 hover:border-white/50 hover:text-white",
  ].join(" ");
}

function formatLastLogin(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  return `${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}`;
}

export function AppLayout() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  // The login page then explains the sign-out (see LoginPage's notice).
  const { warning, stayActive } = useIdleTimeout(() => {
    void signOut("idle");
  });

  // Below 1600px the tabs fold into the ☰ drop-down menu under the header, so
  // the 20px welcome line (names and times vary in length) always shows in full.
  const [menuOpen, setMenuOpen] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);

  // Usage log (Administration › Logs): record each screen opened -- the path
  // only, never the query string, which can hold a patient search. The ref
  // skips repeats (filter changes, StrictMode's double effect run).
  const lastLoggedPath = useRef<string | null>(null);
  useEffect(() => {
    if (!user || lastLoggedPath.current === pathname) return;
    lastLoggedPath.current = pathname;
    recordPageView(pathname).catch(() => {
      // Best effort: a failed usage record must never disturb the page.
    });
  }, [pathname, user]);
  const closeHelp = useCallback(() => setHelpOpen(false), []);

  const visibleModules = headerModules.filter((m) => !m.allowed || canAccess(user, m.allowed));
  const displayName = [user?.firstName, user?.lastName].filter(Boolean).join(" ") || user?.username || "";
  const lastLogin = formatLastLogin(user?.lastLoginAt);
  const pageTitle = moduleTitle(pathname);

  const iconButton =
    "flex h-9 w-9 items-center justify-center rounded text-white/90 transition hover:bg-white/10 hover:text-white";

  return (
    <div className="flex min-h-screen flex-col bg-surface">
      <header className="app-header-bar sticky top-0 z-30 text-white shadow-md print:hidden">
        {/* Three columns with equal-width sides keep the welcome text at the
            true page center; if the tabs outgrow their side it shifts right
            rather than overlapping them. */}
        <div className="flex h-[72px] items-center gap-3 px-4 md:grid md:grid-cols-[1fr_auto_1fr] md:px-6">
          <div className="flex items-center gap-4">
            <HeaderLogo />
            <nav className="hidden items-center gap-3 min-[1600px]:flex" aria-label="Modules">
              <button
                type="button"
                onClick={() => navigate(-1)}
                className="whitespace-nowrap py-1.5 text-xl text-white/90 hover:text-white"
              >
                Go Back
              </button>
              {visibleModules.map((m) => (
                <NavLink key={m.to} to={m.to} end={m.end} className={tabClass}>
                  {m.label}
                </NavLink>
              ))}
            </nav>
          </div>

          <div className="hidden min-w-0 text-center text-xl leading-snug md:block">
            <p className="truncate">
              Welcome {displayName}
              {lastLogin && <>, last login at {lastLogin}</>}
            </p>
            <p className="truncate">{pageTitle}</p>
          </div>

          <div className="ml-auto flex items-center justify-end gap-1">
            <button
              type="button"
              className={`${iconButton} min-[1600px]:hidden`}
              aria-label="Open menu"
              aria-controls="app-menu"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen((v) => !v)}
            >
              <svg
                viewBox="0 0 24 24"
                className="h-6 w-6"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                aria-hidden="true"
              >
                <path d="M4 7h16M4 12h16M4 17h16" />
              </svg>
            </button>
            <button
              type="button"
              onClick={() => void signOut()}
              className="whitespace-nowrap px-2 py-1.5 text-lg text-white/90 hover:text-white sm:text-xl"
            >
              Logout
            </button>
            <button
              type="button"
              onClick={() => window.print()}
              className={`${iconButton} max-sm:hidden`}
              aria-label="Print this page"
              title="Print"
            >
              <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden="true">
                <path d="M7 3h10v4H7zM5 8h14a2 2 0 0 1 2 2v6h-4v4H7v-4H3v-6a2 2 0 0 1 2-2zm4 7v3h6v-3zm8-4.5a1 1 0 1 0 0 2 1 1 0 0 0 0-2z" />
              </svg>
            </button>
            <button
              type="button"
              onClick={() => setHelpOpen(true)}
              className={iconButton}
              aria-label="Help for this page"
              aria-haspopup="dialog"
              title="Help"
            >
              <svg viewBox="0 0 24 24" className="h-6 w-6" aria-hidden="true">
                <circle cx="12" cy="12" r="10" fill="currentColor" />
                <path
                  d="M9.3 9.2a2.8 2.8 0 0 1 5.4 1c0 1.9-2.7 2.3-2.7 4"
                  fill="none"
                  stroke="#0c3d66"
                  strokeWidth="2.2"
                  strokeLinecap="round"
                />
                <circle cx="12" cy="17.6" r="1.3" fill="#0c3d66" />
              </svg>
            </button>
          </div>
        </div>

        {menuOpen && (
          <nav id="app-menu" className="border-t border-white/20 px-4 pb-3 min-[1600px]:hidden" aria-label="Modules">
            <p className="py-2 text-xl text-white/80 md:hidden">
              Welcome {displayName}
              {lastLogin && <>, last login at {lastLogin}</>}
            </p>
            <button
              type="button"
              onClick={() => {
                setMenuOpen(false);
                navigate(-1);
              }}
              className="block w-full py-2 text-left text-xl text-white/90"
            >
              Go Back
            </button>
            {visibleModules.map((m) => (
              <NavLink
                key={m.to}
                to={m.to}
                end={m.end}
                onClick={() => setMenuOpen(false)}
                className={({ isActive }) => `block py-2 text-xl ${isActive ? "font-semibold text-white" : "text-white/90"}`}
              >
                {m.label}
              </NavLink>
            ))}
          </nav>
        )}
      </header>
      {/* Charcoal strip under the header bar. */}
      <div className="h-8 shrink-0 bg-[#5a5a5a] print:hidden" aria-hidden="true" />

      <main className="flex-1 p-4 md:p-6">
        <Outlet />
      </main>
      <LegalFooter className="px-4 py-3" />

      {helpOpen && <HelpDialog helpKey={helpKeyFor(pathname)} onClose={closeHelp} />}

      {warning && (
        <IdleTimeoutWarning
          onStayActive={stayActive}
          onSignOut={() => {
            void signOut();
          }}
        />
      )}
    </div>
  );
}

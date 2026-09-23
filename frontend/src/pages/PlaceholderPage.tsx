/** Stands in for a nav destination not built yet in this SPA foundation
 * (Phase 2 scope is auth + shell + dashboard) -- the corresponding
 * functionality already exists in the Blazor staff UI and the same Api
 * endpoints this app already talks to for patients/auth. */
export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="card">
      <h1 className="mb-2 text-xl font-semibold text-text">{title}</h1>
      <p className="text-sm text-text-muted">This page isn't built yet in this SPA foundation.</p>
    </div>
  );
}

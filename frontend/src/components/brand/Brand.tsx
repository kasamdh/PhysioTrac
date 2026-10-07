// PhysioTrac's own mark: a ring of motion arcs around a pulse line. Kept as
// inline SVG so it scales from the 28px header logo to the login hero.
export function BrandMark({ className = "h-8 w-8", mono = false }: { className?: string; mono?: boolean }) {
  const ring = mono ? "currentColor" : "#c3c8cd";
  const pulse = mono ? "currentColor" : "#13507f";
  const dot = mono ? "currentColor" : "#2bb7a6";
  return (
    <svg viewBox="0 0 64 64" className={className} aria-hidden="true" fill="none">
      <path d="M32 4a28 28 0 0 1 28 28" stroke={ring} strokeWidth="5" strokeLinecap="round" />
      <path d="M60 32a28 28 0 0 1-28 28" stroke={mono ? ring : "#38bdf8"} strokeWidth="5" strokeLinecap="round" />
      <path d="M32 60A28 28 0 0 1 4 32" stroke={ring} strokeWidth="5" strokeLinecap="round" />
      <path d="M4 32A28 28 0 0 1 32 4" stroke={ring} strokeWidth="5" strokeLinecap="round" />
      <path
        d="M13 34h9l4-10 6 18 5-12 3 4h11"
        stroke={pulse}
        strokeWidth="4.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="51" cy="34" r="3.5" fill={dot} />
    </svg>
  );
}

// "Physio" upright + "Trac" italic, the same two-part wordmark rhythm used
// in the header bar, the login hero, and the login panel title.
export function BrandWordmark({ className = "" }: { className?: string }) {
  return (
    <span className={`brand-wordmark ${className}`}>
      Physio<span className="italic">Trac</span>
    </span>
  );
}

export function HeaderLogo() {
  return (
    <span className="flex items-center gap-2 text-white">
      <BrandMark className="h-7 w-7 sm:h-8 sm:w-8" mono />
      <BrandWordmark className="text-2xl sm:text-3xl" />
    </span>
  );
}

export const APP_VERSION = "v1.0.0";

export function LegalFooter({ className = "" }: { className?: string }) {
  return (
    <p className={`text-center text-xs text-text-muted ${className}`}>
      {APP_VERSION} © {new Date().getFullYear()} Source Motion Physical Therapy. All rights reserved. Confidential.
    </p>
  );
}

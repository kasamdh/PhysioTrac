import { useEffect, useId, useRef, useState } from "react";
import { Link } from "react-router-dom";

export type RowMenuItem =
  | { label: string; to: string; danger?: boolean }
  | { label: string; onSelect: () => void; danger?: boolean; disabled?: boolean };

/** The ☰▾ button at the start of a list row, opening that row's actions.
 * The menu is `position: fixed` at the button so a table's horizontal-scroll
 * wrapper can't clip it; scrolling or resizing closes it. */
export function RowMenu({ label, items }: { label: string; items: RowMenuItem[] }) {
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const menuId = useId();
  const open = pos !== null;

  useEffect(() => {
    if (!open) return;
    const close = () => setPos(null);
    const onPointer = (e: PointerEvent) => {
      const target = e.target as Node;
      if (!menuRef.current?.contains(target) && !buttonRef.current?.contains(target)) close();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        close();
        buttonRef.current?.focus();
      }
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    window.addEventListener("scroll", close, true);
    window.addEventListener("resize", close);
    menuRef.current?.querySelector<HTMLElement>("[role=menuitem]")?.focus();
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
      window.removeEventListener("scroll", close, true);
      window.removeEventListener("resize", close);
    };
  }, [open]);

  const toggle = () => {
    if (open) return setPos(null);
    const rect = buttonRef.current!.getBoundingClientRect();
    // Flip upward when there isn't room below for a few items.
    const below = window.innerHeight - rect.bottom > 48 * items.length + 16;
    setPos({ top: below ? rect.bottom + 4 : Math.max(8, rect.top - 4 - 44 * items.length), left: rect.left });
  };

  const itemClass = (danger?: boolean) =>
    `block w-full px-4 py-2.5 text-left text-[15px] whitespace-nowrap hover:bg-primary-light focus:bg-primary-light focus:outline-none disabled:opacity-50 ${
      danger ? "text-danger" : "text-[#333]"
    }`;

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        onClick={toggle}
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        className="flex h-10 w-12 items-center justify-center gap-1 rounded border border-[#c4c4c4] bg-gradient-to-b from-white to-[#e9e9e9] text-[#333] hover:to-[#dcdcdc] focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        <svg viewBox="0 0 16 12" className="h-3 w-4" fill="currentColor" aria-hidden="true">
          <rect y="0" width="16" height="2" rx="1" />
          <rect y="5" width="16" height="2" rx="1" />
          <rect y="10" width="16" height="2" rx="1" />
        </svg>
        <svg viewBox="0 0 8 5" className="h-1.5 w-2" fill="currentColor" aria-hidden="true">
          <path d="M0 0h8L4 5z" />
        </svg>
      </button>
      {pos && (
        <div
          ref={menuRef}
          id={menuId}
          role="menu"
          aria-label={label}
          style={{ top: pos.top, left: pos.left }}
          className="fixed z-50 min-w-48 overflow-hidden rounded border border-[#c4c4c4] bg-white py-1 shadow-lg"
          onKeyDown={(e) => {
            if (e.key !== "ArrowDown" && e.key !== "ArrowUp") return;
            e.preventDefault();
            const all = [...(menuRef.current?.querySelectorAll<HTMLElement>("[role=menuitem]") ?? [])];
            const i = all.indexOf(document.activeElement as HTMLElement);
            all[(i + (e.key === "ArrowDown" ? 1 : all.length - 1)) % all.length]?.focus();
          }}
        >
          {items.map((item) =>
            "to" in item ? (
              <Link key={item.label} to={item.to} role="menuitem" className={itemClass(item.danger)} onClick={() => setPos(null)}>
                {item.label}
              </Link>
            ) : (
              <button
                key={item.label}
                type="button"
                role="menuitem"
                disabled={item.disabled}
                className={itemClass(item.danger)}
                onClick={() => {
                  setPos(null);
                  item.onSelect();
                }}
              >
                {item.label}
              </button>
            ),
          )}
        </div>
      )}
    </>
  );
}

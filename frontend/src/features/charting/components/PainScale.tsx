/** 0-10 numeric pain rating as a row of buttons (tap-friendly), plus Clear. */
export function PainScale({
  label,
  value,
  onChange,
  readOnly,
}: {
  label: string;
  value: number | null;
  onChange: (v: number | null) => void;
  readOnly?: boolean;
}) {
  return (
    <div>
      <p className="mb-1 font-bold text-[#333]">
        {label}
        <span className="ml-2 font-normal text-text-muted">
          {value === null ? "not rated" : `${value}/10`}
        </span>
      </p>
      <div role="group" aria-label={label} className="flex flex-wrap gap-1">
        {Array.from({ length: 11 }, (_, n) => (
          <button
            key={n}
            type="button"
            disabled={readOnly}
            aria-pressed={value === n}
            onClick={() => onChange(n)}
            className={`h-10 w-10 rounded border text-sm ${
              value === n
                ? n >= 7
                  ? "border-danger bg-danger text-white"
                  : n >= 4
                    ? "border-warning bg-warning text-white"
                    : "border-primary bg-primary text-white"
                : "border-[#c4c4c4] bg-white text-[#333] hover:bg-primary-light"
            } disabled:cursor-default`}
          >
            {n}
          </button>
        ))}
        {!readOnly && value !== null && (
          <button
            type="button"
            className="btn-refresh h-10 px-3"
            onClick={() => onChange(null)}
          >
            Clear
          </button>
        )}
      </div>
    </div>
  );
}

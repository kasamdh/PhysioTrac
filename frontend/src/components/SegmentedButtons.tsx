/** Joined row of quick-filter buttons (Recent | Today | Yesterday ...); the
 * selected one is filled navy. */
export function SegmentedButtons<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="seg-group">
      {options.map((o) => (
        <button key={o.value} type="button" className="seg-btn" aria-pressed={o.value === value} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

import { useState, type InputHTMLAttributes } from "react";

const parse = (s: string) =>
  s.trim() === "" || Number.isNaN(Number(s)) ? null : Number(s);

/** A decimal text box that keeps what is being typed ("0.", "-") while
 * reporting the number (or null) on every change, and follows the value
 * when it changes from outside. */
export function DecimalInput({
  value,
  onValue,
  ...props
}: {
  value: number | null | undefined;
  onValue: (v: number | null) => void;
} & Omit<InputHTMLAttributes<HTMLInputElement>, "value" | "onChange">) {
  const [text, setText] = useState(value == null ? "" : String(value));
  const [seen, setSeen] = useState(value ?? null);
  if ((value ?? null) !== seen) {
    setSeen(value ?? null);
    if (parse(text) !== (value ?? null))
      setText(value == null ? "" : String(value));
  }
  return (
    <input
      {...props}
      inputMode="decimal"
      value={text}
      onChange={(e) => {
        setText(e.target.value);
        const v = parse(e.target.value);
        setSeen(v);
        onValue(v);
      }}
    />
  );
}

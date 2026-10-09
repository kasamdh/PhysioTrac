import { formatDate, type OutcomeDefinition, type OutcomeScore } from "./model";

const W = 320;
const H = 150;
const PAD = { left: 34, right: 12, top: 18, bottom: 26 };

/** A measure's scores over time (oldest to newest, evenly spaced), with
 * the baseline as a dashed line. The y-axis is the measure's own range. */
export function TrendChart({
  definition: d,
  history,
}: {
  definition: OutcomeDefinition;
  history: OutcomeScore[];
}) {
  const values = history.map((s) => s.score);
  const lo = d.scoreMax !== null ? d.scoreMin : 0;
  const hi =
    d.scoreMax !== null ? d.scoreMax : Math.max(...values, 1) * 1.25 || 1;
  const x = (i: number) =>
    history.length === 1
      ? (PAD.left + W - PAD.right) / 2
      : PAD.left + (i * (W - PAD.left - PAD.right)) / (history.length - 1);
  const y = (v: number) =>
    PAD.top + (1 - (v - lo) / (hi - lo || 1)) * (H - PAD.top - PAD.bottom);
  const points = history.map((s, i) => `${x(i)},${y(s.score)}`).join(" ");
  const summary = `${d.abbreviation} trend: ${history
    .map((s) => `${s.score} on ${formatDate(s.measuredOn)}`)
    .join(", ")}. ${d.higherIsBetter ? "Higher" : "Lower"} is better.`;

  return (
    <figure className="m-0">
      <svg
        viewBox={`0 0 ${W} ${H}`}
        className="h-auto w-full max-w-md"
        role="img"
        aria-label={summary}
      >
        <line
          x1={PAD.left}
          x2={W - PAD.right}
          y1={y(lo)}
          y2={y(lo)}
          stroke="#ccc"
        />
        <line x1={PAD.left} x2={PAD.left} y1={y(hi)} y2={y(lo)} stroke="#ccc" />
        <text x={PAD.left - 4} y={y(hi) + 4} fontSize="10" textAnchor="end">
          {Math.round(hi)}
        </text>
        <text x={PAD.left - 4} y={y(lo)} fontSize="10" textAnchor="end">
          {lo}
        </text>
        <line
          x1={PAD.left}
          x2={W - PAD.right}
          y1={y(history[0].score)}
          y2={y(history[0].score)}
          stroke="#888"
          strokeDasharray="4 3"
        />
        {history.length > 1 && (
          <polyline
            points={points}
            fill="none"
            stroke="#1565b8"
            strokeWidth="2"
          />
        )}
        {history.map((s, i) => (
          <g key={s.id}>
            <circle cx={x(i)} cy={y(s.score)} r="4" fill="#1565b8">
              <title>{`${formatDate(s.measuredOn)}: ${s.score}`}</title>
            </circle>
            <text x={x(i)} y={y(s.score) - 8} fontSize="10" textAnchor="middle">
              {s.score}
            </text>
          </g>
        ))}
        <text x={PAD.left} y={H - 8} fontSize="10">
          {formatDate(history[0].measuredOn)}
        </text>
        {history.length > 1 && (
          <text x={W - PAD.right} y={H - 8} fontSize="10" textAnchor="end">
            {formatDate(history[history.length - 1].measuredOn)}
          </text>
        )}
      </svg>
      <figcaption className="text-text-muted">
        Dashed line: baseline. {d.higherIsBetter ? "Higher" : "Lower"} is
        better.
      </figcaption>
    </figure>
  );
}

const BAR_WIDTH = 3;
const GAP = 1; // surface gap between bars
const HEIGHT = 24;

function utcDay(daysAgo: number): string {
  const d = new Date();
  d.setUTCHours(0, 0, 0, 0);
  d.setUTCDate(d.getUTCDate() - daysAgo);
  return d.toISOString().slice(0, 10);
}

/**
 * Daily send counts as tiny bars, oldest left, today right (UTC days). Older days use a muted
 * step of the primary color and today the full accent; empty days show a baseline tick.
 * Each day has a native hover tooltip; the exact totals are in the neighbouring Sent columns.
 */
export function Sparkline({ values }: { values: number[] | null | undefined }) {
  if (!values) {
    return (
      <span className="text-xs text-muted-foreground" title="Not available: loading, failed, or the sent-email retention is shorter than 30 days">
        —
      </span>
    );
  }

  const max = Math.max(1, ...values);
  const total = values.reduce((a, b) => a + b, 0);
  const peak = values.indexOf(Math.max(...values));
  const width = values.length * (BAR_WIDTH + GAP) - GAP;
  const summary =
    total === 0
      ? 'No emails sent in the last 30 days'
      : `${total} emails sent in the last 30 days, most on ${utcDay(values.length - 1 - peak)} (${values[peak]})`;

  return (
    <svg width={width} height={HEIGHT} viewBox={`0 0 ${width} ${HEIGHT}`} role="img" aria-label={summary} className="block">
      <title>{summary}</title>
      {values.map((count, i) => {
        const x = i * (BAR_WIDTH + GAP);
        const isToday = i === values.length - 1;
        const barHeight = count === 0 ? 1 : Math.max(2, Math.round((count / max) * HEIGHT));
        return (
          <g key={i}>
            <rect
              x={x}
              y={HEIGHT - barHeight}
              width={BAR_WIDTH}
              height={barHeight}
              rx={count === 0 ? 0 : 1}
              className={count === 0 ? 'fill-muted-foreground/30' : isToday ? 'fill-primary' : 'fill-primary/60'}
            />
            {/* Full-height hit area so each day is easy to hover */}
            <rect x={x - GAP / 2} y={0} width={BAR_WIDTH + GAP} height={HEIGHT} fill="transparent">
              <title>{`${utcDay(values.length - 1 - i)}: ${count} email${count === 1 ? '' : 's'}`}</title>
            </rect>
          </g>
        );
      })}
    </svg>
  );
}

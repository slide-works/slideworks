import type { ChartSlots } from '../slots';
import { DIGIT_EM, SERIES_COLORS, VALUE_FONT, type Scale } from './geometry';
import { formatValue } from './scale';
import styles from './ChartBars.module.css';

type ChartBarsProps = {
  series: ChartSlots['series'];
  slot: number;
  centre: (index: number) => number;
  y: Scale;
};

// The bars of a bar chart: one group per x slot, one bar per series, from the
// zero line to the value. A single series also shows each value over its bar,
// when the label fits the slot.
export function ChartBars({ series, slot, centre, y }: ChartBarsProps) {
  const group = slot * 0.7;
  const gap = series.length > 1 ? 6 : 0;
  const bar = (group - gap * (series.length - 1)) / series.length;
  const showValues = series.length === 1;
  return (
    <g>
      {series.map((s, j) =>
        s.values.map((value, i) => {
          const x = centre(i) - group / 2 + j * (bar + gap);
          const top = Math.min(y(value), y(0));
          const label = formatValue(value);
          return (
            <g key={`${j}-${i}`}>
              <rect
                data-testid="chart-bar"
                data-series={j}
                x={x}
                y={top}
                width={bar}
                height={Math.abs(y(value) - y(0))}
                rx={Math.min(6, bar / 2)}
                style={{ fill: SERIES_COLORS[j] }}
              />
              {showValues && label.length * VALUE_FONT * DIGIT_EM <= slot * 0.95 ? (
                <text
                  data-testid="chart-value"
                  className={styles.value}
                  x={x + bar / 2}
                  y={value >= 0 ? y(value) - 12 : y(value) + VALUE_FONT + 6}
                >
                  {label}
                </text>
              ) : null}
            </g>
          );
        }),
      )}
    </g>
  );
}

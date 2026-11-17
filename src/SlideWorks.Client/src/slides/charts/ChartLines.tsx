import type { ChartSlots } from '../slots';
import { SERIES_COLORS, type Scale } from './geometry';
import styles from './ChartLines.module.css';

type ChartLinesProps = {
  series: ChartSlots['series'];
  centre: (index: number) => number;
  y: Scale;
};

// The lines of a line chart: one per series, through a point at each x slot.
export function ChartLines({ series, centre, y }: ChartLinesProps) {
  return (
    <g>
      {series.map((s, j) => (
        <g key={j} style={{ fill: SERIES_COLORS[j], stroke: SERIES_COLORS[j] }}>
          <path
            data-testid="chart-line"
            data-series={j}
            className={styles.line}
            d={s.values.map((v, i) => `${i === 0 ? 'M' : 'L'}${centre(i)},${y(v)}`).join(' ')}
          />
          {s.values.map((v, i) => (
            <circle
              key={i}
              data-testid="chart-point"
              className={styles.point}
              cx={centre(i)}
              cy={y(v)}
              r={7}
            />
          ))}
        </g>
      ))}
    </g>
  );
}

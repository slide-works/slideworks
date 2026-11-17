import { LABEL_EM, MARKER_FONT, TOP } from './geometry';
import styles from './ChartMarker.module.css';

type ChartMarkerProps = { x: number; label: string; bottom: number; plotRight: number };

// A dashed vertical line with its label at the top, on its right unless it
// would run past the plot's right edge.
export function ChartMarker({ x, label, bottom, plotRight }: ChartMarkerProps) {
  const onLeft = x + 12 + label.length * MARKER_FONT * LABEL_EM > plotRight;
  return (
    <g>
      <line
        data-testid="chart-marker"
        className={styles.marker}
        x1={x}
        x2={x}
        y1={TOP - 40}
        y2={bottom}
      />
      <text
        data-testid="chart-marker-label"
        className={styles.markerLabel}
        x={onLeft ? x - 12 : x + 12}
        y={TOP - 20}
        textAnchor={onLeft ? 'end' : 'start'}
      >
        {label}
      </text>
    </g>
  );
}

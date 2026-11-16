import type { ChartSlots } from '../slots';
import { formatValue, linear, niceTicks } from './scale';
import styles from './ChartPlot.module.css';

// Series colours in order: the accent, the warning orange and the ink.
export const SERIES_COLORS = ['var(--sw-accent)', 'var(--sw-warn)', 'var(--sw-ink)'] as const;

// The plot's height in canvas px; its width is the space the layout gives it.
export const PLOT_HEIGHT = 600;

const TOP = 70;
const RIGHT = 24;
const FONT = 22;
const VALUE_FONT = 24;
const MARKER_FONT = 20;
// Average glyph widths (in em) used to decide where labels go. They err wide,
// so a label that is placed as fitting does fit.
const DIGIT_EM = 0.62;
const LABEL_EM = 0.85;
const TILT = (35 * Math.PI) / 180;

type ChartPlotProps = Pick<ChartSlots, 'kind' | 'xLabels' | 'series' | 'yUnit' | 'marker'> & {
  width: number;
};

// A hand-drawn line or bar chart (D18) in an SVG of `width` × PLOT_HEIGHT
// canvas px: round y ticks with grid lines, one slot per x label, grouped bars
// or lines with points, an optional dashed marker, and SVG text labels. When
// the x labels are too long for their slots they tilt 35°.
export function ChartPlot({ kind, xLabels, series, yUnit, marker, width }: ChartPlotProps) {
  const values = series.flatMap((s) => s.values);
  const low = Math.min(...values);
  const high = Math.max(...values);
  const ticks =
    kind === 'bar' ? niceTicks(Math.min(0, low), Math.max(0, high)) : niceTicks(low, high);
  const tickLabels = ticks.values.map(formatValue);

  const count = xLabels.length;
  const labelWidth = Math.max(...xLabels.map((l) => l.length)) * FONT * LABEL_EM;
  const axisWidth = Math.max(
    60,
    Math.max(...tickLabels.map((l) => l.length)) * FONT * DIGIT_EM + 20,
  );
  const straightSlot = (width - axisWidth - RIGHT) / count;
  const tilt = labelWidth > straightSlot - 12;
  // A tilted first label reaches left of its slot's centre by its length, and
  // by its glyphs' height, turned 35°; the plot moves right until it clears
  // the slide's edge.
  const reach = labelWidth * Math.cos(TILT) + FONT * Math.sin(TILT) + 4;
  const left = tilt
    ? Math.max(axisWidth, (reach - (width - RIGHT) / (2 * count)) / (1 - 1 / (2 * count)))
    : axisWidth;
  const slot = (width - left - RIGHT) / count;
  const bottom = PLOT_HEIGHT - (tilt ? labelWidth * Math.sin(TILT) + FONT + 16 : FONT + 24);
  const y = linear(ticks.lo, ticks.hi, bottom, TOP);
  const centre = (index: number) => left + (index + 0.5) * slot;

  return (
    <svg
      data-testid="chart-plot"
      className={styles.plot}
      viewBox={`0 0 ${width} ${PLOT_HEIGHT}`}
      preserveAspectRatio="xMinYMin meet"
      role="img"
      aria-label={`${kind === 'bar' ? 'Bar' : 'Line'} chart: ${series.map((s) => s.name).join(', ')}`}
    >
      {ticks.values.map((tick, i) => (
        <g key={i}>
          <line
            className={tick === 0 ? styles.axis : styles.grid}
            x1={left}
            x2={width - RIGHT}
            y1={y(tick)}
            y2={y(tick)}
          />
          <text data-testid="chart-tick" className={styles.tick} x={left - 16} y={y(tick) + 8}>
            {tickLabels[i]}
          </text>
        </g>
      ))}
      {yUnit !== undefined ? (
        <text data-testid="chart-unit" className={styles.tick} x={left - 16} y={TOP - 30}>
          {yUnit}
        </text>
      ) : null}
      {kind === 'bar' ? (
        <Bars series={series} slot={slot} centre={centre} y={y} />
      ) : (
        <Lines series={series} centre={centre} y={y} />
      )}
      {xLabels.map((label, i) => (
        <text
          key={i}
          data-testid="chart-x-label"
          className={tilt ? styles.tilted : styles.label}
          x={tilt ? centre(i) + 6 : centre(i)}
          y={tilt ? bottom + 26 : bottom + 36}
          transform={tilt ? `rotate(-35 ${centre(i) + 6} ${bottom + 26})` : undefined}
        >
          {label}
        </text>
      ))}
      {marker !== undefined ? (
        <Marker
          x={left + (marker.x - 0.5) * slot}
          label={marker.label}
          bottom={bottom}
          width={width}
        />
      ) : null}
    </svg>
  );
}

type Scale = (value: number) => number;

function Bars({
  series,
  slot,
  centre,
  y,
}: {
  series: ChartSlots['series'];
  slot: number;
  centre: (index: number) => number;
  y: Scale;
}) {
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

function Lines({
  series,
  centre,
  y,
}: {
  series: ChartSlots['series'];
  centre: (index: number) => number;
  y: Scale;
}) {
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

// A dashed vertical line with its label at the top, on its right unless it
// would run off the plot.
function Marker({
  x,
  label,
  bottom,
  width,
}: {
  x: number;
  label: string;
  bottom: number;
  width: number;
}) {
  const onLeft = x + 12 + label.length * MARKER_FONT * LABEL_EM > width;
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

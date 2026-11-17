import { useId } from 'react';
import type { ChartSlots } from '../slots';
import { ChartBars } from './ChartBars';
import { ChartLines } from './ChartLines';
import { ChartMarker } from './ChartMarker';
import { chartDescription, chartTitle } from './describeChart';
import { DIGIT_EM, FONT, LABEL_EM, PLOT_HEIGHT, RIGHT, TOP, UNIT_EM } from './geometry';
import { linear, niceTicks, tickLabels } from './scale';
import styles from './ChartPlot.module.css';

const TILT = (35 * Math.PI) / 180;

type ChartPlotProps = Pick<ChartSlots, 'kind' | 'xLabels' | 'series' | 'yUnit' | 'marker'> & {
  width: number;
};

// A hand-drawn line or bar chart (D18) in an SVG of `width` × PLOT_HEIGHT
// canvas px: round y ticks with grid lines, one slot per x label, grouped bars
// or lines with points, an optional dashed marker, and SVG text labels. When
// the x labels are too long for their slots they tilt 35°. Its title and
// description carry the data in plain text for screen readers.
export function ChartPlot({ kind, xLabels, series, yUnit, marker, width }: ChartPlotProps) {
  const id = useId();
  const values = series.flatMap((s) => s.values);
  const low = Math.min(...values);
  const high = Math.max(...values);
  const ticks =
    kind === 'bar' ? niceTicks(Math.min(0, low), Math.max(0, high)) : niceTicks(low, high);
  const labels = tickLabels(ticks);

  const count = xLabels.length;
  const labelWidth = Math.max(...xLabels.map((l) => l.length)) * FONT * LABEL_EM;
  // The axis is wide enough for the tick labels and for the unit above them,
  // both end-anchored 16 px left of the plot, so neither runs past x 0.
  const axisWidth = Math.max(
    60,
    Math.max(...labels.map((l) => l.length)) * FONT * DIGIT_EM + 20,
    (yUnit ?? '').length * FONT * UNIT_EM + 20,
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
  const described = { kind, xLabels, series, yUnit, marker };

  return (
    <svg
      data-testid="chart-plot"
      className={styles.plot}
      viewBox={`0 0 ${width} ${PLOT_HEIGHT}`}
      preserveAspectRatio="xMinYMin meet"
      role="img"
      aria-labelledby={`${id}-title`}
      aria-describedby={`${id}-desc`}
    >
      <title id={`${id}-title`}>{chartTitle(described)}</title>
      <desc id={`${id}-desc`}>{chartDescription(described)}</desc>
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
            {labels[i]}
          </text>
        </g>
      ))}
      {yUnit !== undefined ? (
        <text data-testid="chart-unit" className={styles.tick} x={left - 16} y={TOP - 30}>
          {yUnit}
        </text>
      ) : null}
      {kind === 'bar' ? (
        <ChartBars series={series} slot={slot} centre={centre} y={y} />
      ) : (
        <ChartLines series={series} centre={centre} y={y} />
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
        <ChartMarker
          x={left + (marker.x - 0.5) * slot}
          label={marker.label}
          bottom={bottom}
          plotRight={width - RIGHT}
        />
      ) : null}
    </svg>
  );
}

import { BodyCallout } from '../blocks/BodyCallout';
import { Callout } from '../blocks/Callout';
import { SlideHeading } from '../blocks/SlideHeading';
import { ChartPlot } from '../charts/ChartPlot';
import { SERIES_COLORS } from '../charts/geometry';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import styles from './ChartLayout.module.css';

// The body is 1680 px wide; an aside takes 480 px of it, 60 px from the plot.
const BODY_WIDTH = 1680;
const ASIDE_WIDTH = 480;
const GAP = 60;

// A headline over a hand-drawn line or bar chart (D18). With more than one
// series, a legend names them beside the plot, and the caption sits under it
// as an info callout. A callout of its own sits under both, from the start or
// as the slide's only step; the plot shrinks to make room.
export function ChartLayout({ slots, step }: LayoutProps<'chart'>) {
  const { kicker, headline, kind, xLabels, series, yUnit, caption, marker, callout } = slots;
  const legend = series.length > 1;
  const aside = legend || caption !== undefined;
  const columns = (
    <>
      <div className={styles.plot}>
        <ChartPlot
          kind={kind}
          xLabels={xLabels}
          series={series}
          yUnit={yUnit}
          marker={marker}
          width={aside ? BODY_WIDTH - ASIDE_WIDTH - GAP : BODY_WIDTH}
        />
      </div>
      {aside ? (
        <aside className={styles.aside}>
          {legend ? (
            <ul className={styles.legend}>
              {series.map((s, i) => (
                <li key={i} data-testid="chart-legend-item" className={styles.legendItem}>
                  <span className={styles.swatch} style={{ background: SERIES_COLORS[i] }} />
                  {s.name}
                </li>
              ))}
            </ul>
          ) : null}
          {caption !== undefined ? <Callout tone="info" text={caption} /> : null}
        </aside>
      ) : null}
    </>
  );
  const dataAside = aside ? 'true' : undefined;
  return (
    <div data-testid="layout-chart" className="sw-layout">
      <SlideHeading
        layout="chart"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      {callout === undefined ? (
        <div
          data-testid="chart-body"
          data-aside={dataAside}
          className={`sw-body ${styles.columns}`}
        >
          {columns}
        </div>
      ) : (
        <div data-testid="chart-body" data-aside={dataAside} className={`sw-body ${styles.stack}`}>
          <div data-aside={dataAside} className={`${styles.columns} ${styles.fill}`}>
            {columns}
          </div>
          <BodyCallout callout={callout} step={step} at={calloutStep(0, callout)} />
        </div>
      )}
    </div>
  );
}

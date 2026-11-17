import { Callout } from '../blocks/Callout';
import { SlideHeading } from '../blocks/SlideHeading';
import { ChartPlot } from '../charts/ChartPlot';
import { SERIES_COLORS } from '../charts/geometry';
import type { LayoutProps } from '../slots';
import styles from './ChartLayout.module.css';

// The body is 1680 px wide; an aside takes 480 px of it, 60 px from the plot.
const BODY_WIDTH = 1680;
const ASIDE_WIDTH = 480;
const GAP = 60;

// A headline over a hand-drawn line or bar chart (D18). With more than one
// series, a legend names them beside the plot, and the caption sits under it
// as an info callout.
export function ChartLayout({ slots }: LayoutProps<'chart'>) {
  const { kicker, headline, kind, xLabels, series, yUnit, caption, marker } = slots;
  const legend = series.length > 1;
  const aside = legend || caption !== undefined;
  return (
    <div data-testid="layout-chart" className="sw-layout">
      <SlideHeading layout="chart" kicker={kicker} headline={headline} />
      <div
        data-testid="chart-body"
        data-aside={aside ? 'true' : undefined}
        className={`sw-body ${styles.columns}`}
      >
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
      </div>
    </div>
  );
}

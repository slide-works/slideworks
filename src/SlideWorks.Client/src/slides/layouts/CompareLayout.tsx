import type { CompareSide, LayoutProps } from '../slots';
import styles from './CompareLayout.module.css';

// Two panels side by side: the failure mode (bad, red) on the left and the
// success mode (good, green) on the right.
export function CompareLayout({ slots }: LayoutProps<'compare'>) {
  const { kicker, headline, failure, success } = slots;
  return (
    <div data-testid="layout-compare" className={`sw-layout ${styles.layout}`}>
      {kicker !== undefined ? (
        <p data-testid="compare-kicker" className={styles.kicker}>
          {kicker}
        </p>
      ) : null}
      <h2 data-testid="compare-headline" className={styles.headline}>
        {headline}
      </h2>
      <div data-testid="compare-body" className={`sw-body ${styles.body} ${styles.panels}`}>
        <Panel testId="compare-failure" tone="bad" side={failure} />
        <Panel testId="compare-success" tone="good" side={success} />
      </div>
    </div>
  );
}

function Panel({
  testId,
  tone,
  side,
}: {
  testId: string;
  tone: 'good' | 'bad';
  side: CompareSide;
}) {
  return (
    <section data-testid={testId} data-tone={tone} className={styles.panel}>
      <p data-testid="compare-label" className={styles.tag}>
        {side.label}
      </p>
      <h3 data-testid="compare-title" className={styles.title}>
        {side.title}
      </h3>
      {side.sub !== undefined ? (
        <p data-testid="compare-sub" className={styles.sub}>
          {side.sub}
        </p>
      ) : null}
      <ul className={styles.points}>
        {side.points.map((point, i) => (
          <li key={i} data-testid="compare-point">
            {point}
          </li>
        ))}
      </ul>
    </section>
  );
}

import type { CompareSide } from '../slots';
import styles from './ComparePanel.module.css';

type ComparePanelProps = {
  testId: string;
  tone: 'good' | 'bad';
  side: CompareSide;
};

// One compare panel: a tag, a title, an optional sub and the points, coloured
// by its tone.
export function ComparePanel({ testId, tone, side }: ComparePanelProps) {
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

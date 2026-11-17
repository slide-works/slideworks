import { panelTone, toneStyle } from '../blocks/tone';
import type { CompareSide, Tone } from '../slots';
import styles from './ComparePanel.module.css';

type ComparePanelProps = {
  testId: string;
  // The side's tone when it sets none.
  defaultTone: Tone;
  side: CompareSide;
};

// One compare panel: a tag, a title, an optional sub and the points, on its
// tone's surface and ink (soft unless its fill says solid).
export function ComparePanel({ testId, defaultTone, side }: ComparePanelProps) {
  const tone = side.tone ?? defaultTone;
  const fill = side.fill ?? 'soft';
  return (
    <section
      data-testid={testId}
      data-tone={tone}
      data-panel={panelTone(tone)}
      data-fill={fill}
      className={styles.panel}
      style={toneStyle(tone, fill)}
    >
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

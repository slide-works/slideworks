import type { CSSProperties } from 'react';
import { Emphasised } from '../blocks/Emphasised';
import { Icon } from '../blocks/Icon';
import { iconFor } from '../blocks/icons';
import { Stamp } from '../blocks/Stamp';
import { panelTone, tonePair, toneRule, toneStyle } from '../blocks/tone';
import { Reveal } from '../Reveal';
import type { CompareSide, Tone } from '../slots';
import type { Emphasis } from '../slots/blocks';
import styles from './ComparePanel.module.css';

type ComparePanelProps = {
  testId: string;
  // The side's tone when it sets none.
  defaultTone: Tone;
  side: CompareSide;
  // The emphasis entries that belong to this side's line.
  lineSpans: Emphasis[];
  // The slide's step, and the step the stamp appears at (0 shows it from the start).
  step: number;
  stampAt: number;
};

// One compare panel: a tag (with an optional icon), a title, an optional sub,
// then the points, the label-and-value rows or the one large line, an optional
// stamp and an optional footer at the panel's foot, on its tone's surface and
// ink (soft unless its fill says solid or plain).
export function ComparePanel({
  testId,
  defaultTone,
  side,
  lineSpans,
  step,
  stampAt,
}: ComparePanelProps) {
  const tone = side.tone ?? defaultTone;
  const fill = side.fill ?? 'soft';
  const style: CSSProperties =
    fill === 'plain'
      ? ({
          ...toneStyle(tone, fill),
          '--panel-edge': `var(--sw-${toneRule(tone)})`,
          '--panel-tag': `var(--sw-${tonePair(tone, 'soft').ink})`,
        } as CSSProperties)
      : toneStyle(tone, fill);
  const label = (
    <p data-testid="compare-label" className={styles.tag}>
      {side.label}
    </p>
  );
  return (
    <section
      data-testid={testId}
      data-tone={tone}
      data-panel={panelTone(tone)}
      data-fill={fill}
      className={styles.panel}
      style={style}
    >
      {side.icon !== undefined && iconFor(side.icon) !== undefined ? (
        <div data-testid="compare-tag-row" className={styles.tagRow}>
          <Icon name={side.icon} size={18} />
          {label}
        </div>
      ) : (
        label
      )}
      <h3 data-testid="compare-title" className={styles.title}>
        {side.title}
      </h3>
      {side.sub !== undefined ? (
        <p data-testid="compare-sub" className={styles.sub}>
          {side.sub}
        </p>
      ) : null}
      {side.points !== undefined ? (
        <ul className={styles.points}>
          {side.points.map((point, i) => (
            <li key={i} data-testid="compare-point">
              {point}
            </li>
          ))}
        </ul>
      ) : null}
      {side.rows !== undefined ? (
        <dl data-testid="compare-rows" className={styles.rows}>
          {side.rows.map((row, i) => (
            <div key={i} data-testid="compare-row" className={styles.row}>
              <dt data-testid="compare-row-label" className={styles.rowLabel}>
                {row.label}
              </dt>
              <dd data-testid="compare-row-value" className={styles.rowValue}>
                {row.value}
              </dd>
            </div>
          ))}
        </dl>
      ) : null}
      {side.line !== undefined ? (
        <p data-testid="compare-line" className={styles.line}>
          <Emphasised text={side.line} spans={lineSpans} />
        </p>
      ) : null}
      {side.stamp !== undefined ? (
        <Reveal at={stampAt} step={step} className={styles.stampSlot}>
          <Stamp text={side.stamp} placement="inline" />
        </Reveal>
      ) : null}
      {side.footer !== undefined ? (
        <div className={styles.footerSlot}>
          <p data-testid="compare-footer" className={styles.footer}>
            {side.footer}
          </p>
        </div>
      ) : null}
    </section>
  );
}

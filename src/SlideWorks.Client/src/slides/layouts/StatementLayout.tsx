import { useRef } from 'react';
import { Emphasised } from '../blocks/Emphasised';
import { Kicker } from '../blocks/Kicker';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './StatementLayout.module.css';

// The sentence is set at 210 px in at most three shown lines, the validator's
// limit, and shrinks to keep them, never below 110 px. It also keeps to the
// height left under the kicker and over the sub line.
const CONTENT_HEIGHT = 846;
const SUB_GAP = 44;

function sentenceHeight(heading: HTMLElement): number {
  const parent = heading.parentElement;
  const kicker = parent?.querySelector<HTMLElement>('[data-testid="statement-kicker"]');
  const sub = parent?.querySelector<HTMLElement>('[data-testid="statement-sub"]');
  const above = kicker === null || kicker === undefined ? 0 : kicker.offsetHeight + 22;
  const below = sub === null || sub === undefined ? 0 : SUB_GAP + sub.offsetHeight;
  return CONTENT_HEIGHT - above - below;
}

const STATEMENT_FIT: FitBudget = {
  start: 210,
  floor: 110,
  lines: 3,
  lineHeight: 1,
  maxHeight: sentenceHeight,
};

// One big sentence that carries the slide, light or dark. Its \n breaks start
// new lines and emphasis is applied per written line. With reveal 'one-by-one'
// the sub line arrives at step 1 and keeps its space until then. It draws no
// tracker.
export function StatementLayout({ slots, step }: LayoutProps<'statement'>) {
  const { kicker, statement, sub, reveal } = slots;
  const lines = statement.split('\n');
  const spans = emphasisFor(slots, 'statement');
  const headingRef = useRef<HTMLHeadingElement>(null);
  useFitText(headingRef, STATEMENT_FIT, `${statement}\n${sub ?? ''}`);
  const subLine =
    sub !== undefined ? (
      <p data-testid="statement-sub" className={styles.sub}>
        <Emphasised text={sub} spans={emphasisFor(slots, 'sub')} />
      </p>
    ) : null;
  return (
    <div data-testid="layout-statement" className={`sw-layout ${styles.statement}`}>
      <Kicker testId="statement-kicker" text={kicker} />
      <h2 ref={headingRef} data-testid="statement-text" className={styles.heading}>
        {lines.map((line, index) => (
          <span key={index} data-testid="statement-line" className={styles.line}>
            <Emphasised text={line} spans={spans} />
          </span>
        ))}
      </h2>
      {reveal === 'one-by-one' && subLine !== null ? (
        <Reveal at={1} step={step}>
          {subLine}
        </Reveal>
      ) : (
        subLine
      )}
    </div>
  );
}

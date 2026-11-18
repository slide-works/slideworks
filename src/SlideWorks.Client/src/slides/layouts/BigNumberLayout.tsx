import { useRef } from 'react';
import { BodyCallout } from '../blocks/BodyCallout';
import { Emphasised } from '../blocks/Emphasised';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './BigNumberLayout.module.css';

// The value is set at 300 px and shrinks to fit its 820 px column, never below
// 120 px: six of the widest characters still fit the column there.
const VALUE_FIT: FitBudget = { start: 300, floor: 120, width: true };

// The caption and detail are set from the text column's font size: a 40 px
// caption over a 28 px detail. Capitals or wide letters at the longest lengths
// run taller than the body, so the column shrinks until it fits the body's
// height (the grid, which takes the space left under the headline), never
// below a 28 px caption.
const TEXT_FIT: FitBudget = { start: 40, floor: 28, maxHeight: bodyHeight };

function bodyHeight(text: HTMLElement): number {
  return text.parentElement?.clientHeight ?? Infinity;
}

// '11 wks' → ['11', ' wks'], '42%' → ['42', '%'], '$4.2M' → ['$4.2', 'M'].
// A value without a digit is all number.
function splitValue(value: string): [string, string] {
  const match = /^(\D*\d[\d.,]*)(.*)$/s.exec(value);
  return match ? [match[1], match[2]] : [value, ''];
}

// One number that carries the slide, with a caption, a detail and a source.
// The value's unit is drawn smaller than the number. With reveal 'one-by-one'
// the detail arrives at step 1 and keeps its space until then. An optional
// callout sits under the number and text, from the start or, when its reveal
// is 'last', as the final step.
export function BigNumberLayout({ slots, step }: LayoutProps<'big-number'>) {
  const { kicker, headline, value, caption, detail, source, reveal, callout } = slots;
  const detailStep = reveal === 'one-by-one' && detail !== undefined ? 1 : 0;
  const [number, unit] = splitValue(value);
  const valueRef = useRef<HTMLSpanElement>(null);
  const textRef = useRef<HTMLDivElement>(null);
  useFitText(valueRef, VALUE_FIT, value);
  useFitText(textRef, TEXT_FIT, `${caption}\n${detail ?? ''}`);
  const detailParagraph =
    detail !== undefined ? (
      <p data-testid="big-number-detail" className={styles.detail}>
        {detail}
      </p>
    ) : null;
  const content = (
    <>
      <div data-testid="big-number-column" className={styles.column}>
        <span ref={valueRef} data-testid="big-number-value" className={styles.value}>
          {number}
          {unit !== '' ? <small className={styles.unit}>{unit}</small> : null}
        </span>
      </div>
      <div ref={textRef} data-testid="big-number-text" className={styles.text}>
        <p data-testid="big-number-caption" className={styles.caption}>
          <Emphasised text={caption} spans={emphasisFor(slots, 'caption')} />
        </p>
        {detailStep > 0 ? (
          <Reveal at={detailStep} step={step}>
            {detailParagraph}
          </Reveal>
        ) : (
          detailParagraph
        )}
      </div>
    </>
  );
  return (
    <div data-testid="layout-big-number" className="sw-layout">
      <SlideHeading
        layout="big-number"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      {callout === undefined ? (
        <div className={`sw-body ${styles.grid}`}>{content}</div>
      ) : (
        <div className={`sw-body ${styles.stack}`}>
          <div className={`${styles.grid} ${styles.fill}`}>{content}</div>
          <BodyCallout callout={callout} step={step} at={calloutStep(detailStep, callout)} />
        </div>
      )}
      {source !== undefined ? (
        <p data-testid="big-number-source" className={styles.source}>
          {`Source: ${source}`}
        </p>
      ) : null}
    </div>
  );
}

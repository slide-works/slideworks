import { useRef } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
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
// The value's unit is drawn smaller than the number.
export function BigNumberLayout({ slots }: LayoutProps<'big-number'>) {
  const { kicker, headline, value, caption, detail, source } = slots;
  const [number, unit] = splitValue(value);
  const valueRef = useRef<HTMLSpanElement>(null);
  const textRef = useRef<HTMLDivElement>(null);
  useFitText(valueRef, VALUE_FIT, value);
  useFitText(textRef, TEXT_FIT, `${caption}\n${detail ?? ''}`);
  return (
    <div data-testid="layout-big-number" className="sw-layout">
      <SlideHeading layout="big-number" kicker={kicker} headline={headline} />
      <div className={`sw-body ${styles.grid}`}>
        <div data-testid="big-number-column" className={styles.column}>
          <span ref={valueRef} data-testid="big-number-value" className={styles.value}>
            {number}
            {unit !== '' ? <small className={styles.unit}>{unit}</small> : null}
          </span>
        </div>
        <div ref={textRef} data-testid="big-number-text" className={styles.text}>
          <p data-testid="big-number-caption" className={styles.caption}>
            {caption}
          </p>
          {detail !== undefined ? (
            <p data-testid="big-number-detail" className={styles.detail}>
              {detail}
            </p>
          ) : null}
        </div>
      </div>
      {source !== undefined ? (
        <p data-testid="big-number-source" className={styles.source}>
          {`Source: ${source}`}
        </p>
      ) : null}
    </div>
  );
}

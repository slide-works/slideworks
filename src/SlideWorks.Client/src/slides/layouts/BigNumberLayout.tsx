import { useLayoutEffect, useRef } from 'react';
import type { LayoutProps } from '../slots';
import styles from './BigNumberLayout.module.css';

// The value's size in the mockup, and the smallest it shrinks to. Six of the
// widest characters still fit the 820 px column at the floor.
const VALUE_PX = 300;
const VALUE_FLOOR_PX = 120;

// '11 wks' → ['11', ' wks'], '42%' → ['42', '%'], '$4.2M' → ['$4.2', 'M'].
// A value without a digit is all number.
function splitValue(value: string): [string, string] {
  const match = /^(\D*\d[\d.,]*)(.*)$/s.exec(value);
  return match ? [match[1], match[2]] : [value, ''];
}

// Sets the value's font size so it fits its column: the mockup's size when it
// fits, otherwise scaled down in proportion, never below the floor. It compares
// the value's width with the column's, both measured under the same transform,
// so the player's scaling doesn't change the result. It fits again once the
// web fonts have loaded.
function useFitToColumn(value: string) {
  const columnRef = useRef<HTMLDivElement>(null);
  const valueRef = useRef<HTMLSpanElement>(null);

  useLayoutEffect(() => {
    let active = true;
    const fit = () => {
      const column = columnRef.current;
      const text = valueRef.current;
      if (!active || column === null || text === null) return;
      const room = column.getBoundingClientRect().width;
      const width = () => text.getBoundingClientRect().width;
      let size = VALUE_PX;
      text.style.fontSize = `${size}px`;
      if (width() > room) {
        size = Math.max(VALUE_FLOOR_PX, Math.floor((VALUE_PX * room) / width()));
        text.style.fontSize = `${size}px`;
        // Rounding and kerning can leave a pixel or two over.
        while (width() > room && size > VALUE_FLOOR_PX) {
          size -= 2;
          text.style.fontSize = `${size}px`;
        }
      }
      text.dataset.fontSize = String(size);
    };
    fit();
    void document.fonts.ready.then(fit);
    return () => {
      active = false;
    };
  }, [value]);

  return { columnRef, valueRef };
}

// One number that carries the slide, with a caption, a detail and a source.
// The value's unit is drawn smaller than the number.
export function BigNumberLayout({ slots }: LayoutProps<'big-number'>) {
  const { kicker, headline, value, caption, detail, source } = slots;
  const [number, unit] = splitValue(value);
  const { columnRef, valueRef } = useFitToColumn(value);
  return (
    <div data-testid="layout-big-number" className={`sw-layout ${styles.body}`}>
      {kicker !== undefined ? (
        <p data-testid="big-number-kicker" className={styles.kicker}>
          {kicker}
        </p>
      ) : null}
      <h2 data-testid="big-number-headline" className={styles.headline}>
        {headline}
      </h2>
      <div className={`sw-body ${styles.grid}`}>
        <div ref={columnRef} data-testid="big-number-column" className={styles.column}>
          <span ref={valueRef} data-testid="big-number-value" className={styles.value}>
            {number}
            {unit !== '' ? <small className={styles.unit}>{unit}</small> : null}
          </span>
        </div>
        <div className={styles.text}>
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

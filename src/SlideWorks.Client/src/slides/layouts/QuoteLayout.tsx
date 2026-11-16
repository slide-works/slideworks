import { useRef } from 'react';
import headingStyles from '../blocks/SlideHeading.module.css';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './QuoteLayout.module.css';

// The quote is set at 62 px in at most seven lines: 280 characters of prose fit
// at that size. Capitals or wide letters run longer, so the quote then shrinks
// until it fits seven lines, never below 36 px.
const QUOTE_FIT: FitBudget = { start: 62, floor: 36, lines: 7, lineHeight: 1.22 };

// A quotation with its attribution. The slot holds the words only, so the
// layout adds the quotation marks. The kicker is the shared content kicker; a
// quote has no headline, so it takes SlideHeading's kicker style, not the block.
export function QuoteLayout({ slots }: LayoutProps<'quote'>) {
  const quoteRef = useRef<HTMLQuoteElement>(null);
  useFitText(quoteRef, QUOTE_FIT, slots.quote);
  return (
    <div data-testid="layout-quote" className="sw-layout">
      {slots.kicker !== undefined ? (
        <p data-testid="quote-kicker" className={headingStyles.kicker}>
          {slots.kicker}
        </p>
      ) : null}
      <figure className={styles.figure}>
        <blockquote ref={quoteRef} data-testid="quote-text" className={styles.quote}>
          {`“${slots.quote}”`}
        </blockquote>
        <figcaption data-testid="quote-attribution" className={styles.attribution}>
          {slots.attribution}
        </figcaption>
      </figure>
    </div>
  );
}

import type { LayoutProps } from '../slots';
import styles from './QuoteLayout.module.css';

// A quotation with its attribution. The slot holds the words only, so the
// layout adds the quotation marks.
export function QuoteLayout({ slots }: LayoutProps<'quote'>) {
  return (
    <div data-testid="layout-quote" className={styles.body}>
      {slots.kicker !== undefined ? (
        <p data-testid="quote-kicker" className={styles.kicker}>
          {slots.kicker}
        </p>
      ) : null}
      <figure className={styles.figure}>
        <blockquote data-testid="quote-text" className={styles.quote}>
          {`“${slots.quote}”`}
        </blockquote>
        <figcaption data-testid="quote-attribution" className={styles.attribution}>
          {slots.attribution}
        </figcaption>
      </figure>
    </div>
  );
}

import type { LayoutProps } from '../slots';
import styles from './CloseLayout.module.css';

// The closing slide (dark): a short headline whose \n breaks start new lines,
// an optional sub in a highlighted box, and an optional author.
export function CloseLayout({ slots }: LayoutProps<'close'>) {
  const lines = slots.headline.split('\n');
  return (
    <div data-testid="layout-close" className={`sw-layout ${styles.close}`}>
      <h2 data-testid="close-headline" className={styles.heading}>
        {lines.map((line, index) => (
          <span key={index} data-testid="close-line" className={styles.line}>
            {line}
          </span>
        ))}
      </h2>
      {slots.sub !== undefined ? (
        <p data-testid="close-sub" className={styles.sub}>
          {slots.sub}
        </p>
      ) : null}
      {slots.author !== undefined ? (
        <p data-testid="close-author" className={styles.byline}>
          {slots.author}
        </p>
      ) : null}
    </div>
  );
}

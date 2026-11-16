import { Stamp } from '../blocks/Stamp';
import type { LayoutProps } from '../slots';
import styles from './TitleLayout.module.css';

// The opening slide (dark): kicker, title, subtitle, tagline, a byline of the
// author and affiliation, and an optional stamp in the top right corner.
export function TitleLayout({ slots }: LayoutProps<'title'>) {
  const { kicker, title, subtitle, tagline, author, affiliation, stamp } = slots;
  return (
    <div data-testid="layout-title" className={styles.title}>
      {stamp !== undefined ? <Stamp text={stamp} /> : null}
      <div className={styles.dots} aria-hidden="true">
        <i />
        <i />
        <i />
      </div>
      {kicker !== undefined ? (
        <p data-testid="title-kicker" className={styles.kicker}>
          {kicker}
        </p>
      ) : null}
      <h1 data-testid="title-title" className={styles.heading}>
        {title}
      </h1>
      {subtitle !== undefined ? (
        <p data-testid="title-subtitle" className={styles.subtitle}>
          {subtitle}
        </p>
      ) : null}
      {tagline !== undefined ? (
        <p data-testid="title-tagline" className={styles.tagline}>
          {tagline}
        </p>
      ) : null}
      {author !== undefined || affiliation !== undefined ? (
        <p data-testid="title-byline" className={styles.byline}>
          {author !== undefined ? <span>{author}</span> : null}
          {author !== undefined && affiliation !== undefined ? (
            <i className={styles.rule} aria-hidden="true" />
          ) : null}
          {affiliation !== undefined ? <span>{affiliation}</span> : null}
        </p>
      ) : null}
    </div>
  );
}

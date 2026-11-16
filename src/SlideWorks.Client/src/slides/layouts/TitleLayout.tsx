import { useRef } from 'react';
import { Stamp } from '../blocks/Stamp';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './TitleLayout.module.css';

// The title is set at 168 px in at most two lines. Wide letters can wrap a
// title the validator accepts onto a third line, so it then shrinks until it
// fits two, never below 96 px (a 34-character title of W fits two lines there).
const TITLE_FIT: FitBudget = { start: 168, floor: 96, lines: 2, lineHeight: 0.95 };

// The subtitle is set at 52 px in at most three lines. A 100-character subtitle
// of capital W takes four there and pushes the byline past y 996, so it then
// shrinks until it fits three, never below 36 px (where any 100 characters do).
const SUBTITLE_FIT: FitBudget = { start: 52, floor: 36, lines: 3, lineHeight: 1.15 };

// The opening slide (dark): kicker, title, subtitle, tagline, a byline of the
// author and affiliation, and an optional stamp in the top right corner.
export function TitleLayout({ slots }: LayoutProps<'title'>) {
  const { kicker, title, subtitle, tagline, author, affiliation, stamp } = slots;
  const titleRef = useRef<HTMLHeadingElement>(null);
  const subtitleRef = useRef<HTMLParagraphElement>(null);
  useFitText(titleRef, TITLE_FIT, title);
  useFitText(subtitleRef, SUBTITLE_FIT, subtitle ?? '');
  return (
    <div data-testid="layout-title" className={`sw-layout ${styles.title}`}>
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
      <h1 ref={titleRef} data-testid="title-title" className={styles.heading}>
        {title}
      </h1>
      {subtitle !== undefined ? (
        <p ref={subtitleRef} data-testid="title-subtitle" className={styles.subtitle}>
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

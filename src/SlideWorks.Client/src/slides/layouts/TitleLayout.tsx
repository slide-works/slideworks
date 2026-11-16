import { useLayoutEffect, useRef } from 'react';
import { Stamp } from '../blocks/Stamp';
import type { LayoutProps } from '../slots';
import styles from './TitleLayout.module.css';

// The title is set at 168 px in at most two lines. Wide letters can wrap a
// title the validator accepts onto a third line, so it then steps down in size
// until it fits two, never below the floor.
const TITLE_PX = 168;
const TITLE_FLOOR_PX = 96;
const TITLE_LINE_HEIGHT = 0.95;
const TITLE_LINES = 2;

// Fits the title to two lines. offsetHeight is in layout pixels, so the
// player's scaling doesn't change the line count. It fits again once the web
// fonts have loaded.
function useTwoLineTitle(title: string) {
  const ref = useRef<HTMLHeadingElement>(null);

  useLayoutEffect(() => {
    let active = true;
    const fit = () => {
      const heading = ref.current;
      if (!active || heading === null) return;
      const lines = (size: number) => Math.round(heading.offsetHeight / (size * TITLE_LINE_HEIGHT));
      let size = TITLE_PX;
      heading.style.fontSize = `${size}px`;
      while (lines(size) > TITLE_LINES && size > TITLE_FLOOR_PX) {
        size = Math.max(TITLE_FLOOR_PX, size - 8);
        heading.style.fontSize = `${size}px`;
      }
      heading.dataset.fontSize = String(size);
    };
    fit();
    void document.fonts.ready.then(fit);
    return () => {
      active = false;
    };
  }, [title]);

  return ref;
}

// The opening slide (dark): kicker, title, subtitle, tagline, a byline of the
// author and affiliation, and an optional stamp in the top right corner.
export function TitleLayout({ slots }: LayoutProps<'title'>) {
  const { kicker, title, subtitle, tagline, author, affiliation, stamp } = slots;
  const titleRef = useTwoLineTitle(title);
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

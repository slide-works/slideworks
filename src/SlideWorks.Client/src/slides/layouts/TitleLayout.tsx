import { useRef } from 'react';
import { Stamp } from '../blocks/Stamp';
import { OrnamentDots } from '../chrome/layers/Ornament';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './TitleLayout.module.css';

// The title is set at 168 px in at most two lines. Wide letters can wrap a
// title the validator accepts onto a third line, so it then shrinks until it
// fits two, never below 96 px (a 34-character title of W fits two lines there).
const TITLE_FIT: FitBudget = { start: 168, floor: 96, lines: 2, lineHeight: 0.95 };

// The stack (dots to byline, not the stamp) may be 846 px tall: from the
// content top at y 150 to y 996, above the footer.
const STACK_HEIGHT = 846;

// How tall a slot in the stack may be: its own height plus the room the whole
// stack leaves, in layout px. A prose title keeps 168 px and two lines, so the
// slots under it can't each keep a fixed budget.
function roomLeft(element: HTMLElement): number {
  const parent = element.parentElement;
  if (parent === null) return Infinity;
  const stack = [...parent.children].filter(
    (child): child is HTMLElement =>
      child instanceof HTMLElement && getComputedStyle(child).position !== 'absolute',
  );
  const first = stack[0];
  const last = stack[stack.length - 1];
  const used = last.offsetTop + last.offsetHeight - first.offsetTop;
  return element.offsetHeight + STACK_HEIGHT - used;
}

// The subtitle is set at 52 px in at most three lines and the room left. A
// 100-character subtitle of capital W takes four lines there, and in capitals
// under a prose title three lines push the byline past y 996, so it then
// shrinks until it fits, never below 36 px (where any 100 characters take three).
const SUBTITLE_FIT: FitBudget = {
  start: 52,
  floor: 36,
  lines: 3,
  lineHeight: 1.15,
  maxHeight: roomLeft,
};

// The tagline is set at 34 px in at most three lines and the room left after
// the subtitle's fit. Words of capital W in the tagline and byline still push
// the byline past y 996 with the subtitle at 36 px, so it then shrinks too,
// never below 26 px.
const TAGLINE_FIT: FitBudget = {
  start: 34,
  floor: 26,
  lines: 3,
  lineHeight: 1.3,
  maxHeight: roomLeft,
};

// The byline is set at 28 px and the room left after the tagline's fit. Two
// 60-character spans of capital W take two lines at 28 px, and under a prose
// title with the subtitle and tagline at their floors that ended the slide at
// y 1009.7, so it shrinks too, never below 22 px.
const BYLINE_FIT: FitBudget = {
  start: 28,
  floor: 22,
  lines: 2,
  lineHeight: 1.2,
  maxHeight: roomLeft,
};

// The opening slide (dark): kicker, title, subtitle, tagline, a byline of the
// author and affiliation, and an optional stamp in the top right corner.
export function TitleLayout({ slots }: LayoutProps<'title'>) {
  const { kicker, title, subtitle, tagline, author, affiliation, stamp } = slots;
  const titleRef = useRef<HTMLHeadingElement>(null);
  const subtitleRef = useRef<HTMLParagraphElement>(null);
  const taglineRef = useRef<HTMLParagraphElement>(null);
  const bylineRef = useRef<HTMLParagraphElement>(null);
  // The room left depends on every slot in the stack, so the room fits run
  // again when any of them changes: the subtitle first, then the tagline.
  const stack = [kicker, title, subtitle, tagline, author, affiliation].join('\n');
  useFitText(titleRef, TITLE_FIT, title);
  useFitText(subtitleRef, SUBTITLE_FIT, stack);
  useFitText(taglineRef, TAGLINE_FIT, stack);
  useFitText(bylineRef, BYLINE_FIT, stack);
  return (
    <div data-testid="layout-title" className={`sw-layout ${styles.title}`}>
      {stamp !== undefined ? <Stamp text={stamp} /> : null}
      <div className={styles.dots} aria-hidden="true">
        <OrnamentDots />
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
        <p ref={taglineRef} data-testid="title-tagline" className={styles.tagline}>
          {tagline}
        </p>
      ) : null}
      {author !== undefined || affiliation !== undefined ? (
        <p ref={bylineRef} data-testid="title-byline" className={styles.byline}>
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

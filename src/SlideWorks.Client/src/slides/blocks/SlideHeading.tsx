import { useRef } from 'react';
import { useFitText } from '../useFitText';
import type { LayoutName } from '../slots';
import type { Emphasis } from '../slots/blocks';
import { Emphasised } from './Emphasised';
import { Kicker } from './Kicker';
import styles from './SlideHeading.module.css';

type SlideHeadingProps = {
  // The layout it heads; its test ids are `<layout>-kicker` and `<layout>-headline`.
  layout: LayoutName;
  kicker?: string;
  headline: string;
  // The headline's phrases to draw in a tone (emphasisFor(slots, 'headline')).
  emphasis?: Emphasis[];
  // The most lines the headline may take (default 2); image beside its picture keeps 3.
  lines?: number;
};

// The headline is set at 64 px and shrinks to at most `lines` lines, never below 48 px. The server
// (HeadingRules) refuses a headline that wraps to more lines at 48 px, so the floor always fits. Keep
// the two floors the same.
const HEADLINE_START = 64;
const HEADLINE_FLOOR = 48;
const HEADLINE_LINE_HEIGHT = 1.08;

// The kicker and headline every content layout opens with. It renders two
// siblings, not a wrapper, so they stay flow children of the layout's root
// (.sw-layout) with the body (.sw-body) under them. Without emphasis the
// headline is its bare text.
export function SlideHeading({ layout, kicker, headline, emphasis, lines = 2 }: SlideHeadingProps) {
  const ref = useRef<HTMLHeadingElement>(null);
  useFitText(
    ref,
    { start: HEADLINE_START, floor: HEADLINE_FLOOR, lines, lineHeight: HEADLINE_LINE_HEIGHT },
    headline,
  );
  return (
    <>
      <Kicker testId={`${layout}-kicker`} text={kicker} />
      <h2 ref={ref} data-testid={`${layout}-headline`} className={styles.headline}>
        <Emphasised text={headline} spans={emphasis} />
      </h2>
    </>
  );
}

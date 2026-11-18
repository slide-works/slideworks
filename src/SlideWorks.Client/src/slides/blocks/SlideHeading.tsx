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
};

// The kicker and headline every content layout opens with. It renders two
// siblings, not a wrapper, so they stay flow children of the layout's root
// (.sw-layout) with the body (.sw-body) under them. Without emphasis the
// headline is its bare text.
export function SlideHeading({ layout, kicker, headline, emphasis }: SlideHeadingProps) {
  return (
    <>
      <Kicker testId={`${layout}-kicker`} text={kicker} />
      <h2 data-testid={`${layout}-headline`} className={styles.headline}>
        <Emphasised text={headline} spans={emphasis} />
      </h2>
    </>
  );
}

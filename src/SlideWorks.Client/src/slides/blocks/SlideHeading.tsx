import type { LayoutName } from '../slots';
import styles from './SlideHeading.module.css';

type SlideHeadingProps = {
  // The layout it heads; its test ids are `<layout>-kicker` and `<layout>-headline`.
  layout: LayoutName;
  kicker?: string;
  headline: string;
};

// The kicker and headline every content layout opens with. It renders two
// siblings, not a wrapper, so they stay flow children of the layout's root
// (.sw-layout) with the body (.sw-body) under them.
export function SlideHeading({ layout, kicker, headline }: SlideHeadingProps) {
  return (
    <>
      {kicker !== undefined ? (
        <p data-testid={`${layout}-kicker`} className={styles.kicker}>
          {kicker}
        </p>
      ) : null}
      <h2 data-testid={`${layout}-headline`} className={styles.headline}>
        {headline}
      </h2>
    </>
  );
}

import { GUEST_TEST_IDS } from './testIds';

// The card below the mirrored slide. SW-5 has no activities, so it is always the waiting card; an interactive slide
// (SW-6 onwards) takes it over (D3).
export function ActivitySlot() {
  // TODO: the real card, styled to the mockups, with the activity slot filled in once an activity exists.
  return (
    <section data-testid={GUEST_TEST_IDS.activity} aria-label="Activity">
      <p data-testid={GUEST_TEST_IDS.activityWaiting}>Nothing to answer yet</p>
    </section>
  );
}

import { GUEST_TEST_IDS } from './testIds';

// The card below the mirrored slide. SW-5 has no activities, so it is always the waiting card; an interactive slide
// (SW-6 onwards) takes it over (D3).
export function ActivitySlot() {
  // TODO: show the activity for an interactive slide here once an activity exists; until then this is always the waiting card.
  return (
    <section
      data-testid={GUEST_TEST_IDS.activity}
      aria-label="Activity"
      className="min-h-12 rounded-xl border border-white/15 bg-white/5 px-4 py-4"
    >
      <p data-testid={GUEST_TEST_IDS.activityWaiting} className="text-base text-white/80">
        Nothing to answer yet
      </p>
    </section>
  );
}

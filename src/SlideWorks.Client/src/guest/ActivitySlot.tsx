import { GUEST_TEST_IDS } from './testIds';

const DOT_DELAYS = ['', '[animation-delay:200ms]', '[animation-delay:400ms]'];

// The card below the mirrored slide: it fills the space under the progress. SW-5 has no activities, so it is always the
// waiting card; an interactive slide (SW-6 onwards) takes it over (D3).
export function ActivitySlot() {
  // TODO: show the activity for an interactive slide here once an activity exists; until then this is always the waiting card.
  return (
    <section
      data-testid={GUEST_TEST_IDS.activity}
      aria-label="Activity"
      className="flex min-h-24 flex-1 flex-col items-center justify-center gap-2.5 rounded-[14px] border border-dashed border-white/15 px-4 py-4 text-center"
    >
      <div aria-hidden className="flex gap-1.5">
        {DOT_DELAYS.map((delay) => (
          <i
            key={delay}
            className={`size-[7px] animate-pulse rounded-full bg-(--sw-accent-glow) motion-reduce:animate-none ${delay}`}
          />
        ))}
      </div>
      <p data-testid={GUEST_TEST_IDS.activityWaiting} className="font-head text-[15px] font-bold">
        Nothing to answer yet
      </p>
      <p className="text-[13px] leading-snug text-(--sw-night-2)">
        Polls and questions appear here when the presenter gets to them. Keep this page open.
      </p>
    </section>
  );
}

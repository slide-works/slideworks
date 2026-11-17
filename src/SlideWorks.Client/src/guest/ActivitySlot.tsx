import { PollAnswerLive } from '../polls/guest/PollAnswerLive';
import { PollCard } from '../polls/guest/PollCard';
import type { PollActivity } from '../polls/types';
import { useAudiencePolls } from './AudiencePollsContext';
import { GUEST_TEST_IDS } from './testIds';
import type { AudienceStatus } from './useAudience';

const DOT_DELAYS = ['', '[animation-delay:200ms]', '[animation-delay:400ms]'];

export type ActivitySlotProps = {
  // The join code the page is on, for the poll's GET and PUT.
  code: string;
  // The audience state's activity: the poll on screen, or null for a slide with nothing to answer.
  activity: PollActivity | null;
  // The audience connection's status, so an unsent pick waits while it is reconnecting or offline.
  status: AudienceStatus;
  // The presenter's name, as the card's copy names them ("Private: Sam sees totals only").
  presenter: string;
};

// The card below the mirrored slide: it fills the space under the progress. A poll on screen takes it over with its
// card (SW-6 V6), which gets the audience connection's poll channel from GuestLive; any other slide shows SW-5's
// waiting card.
export function ActivitySlot({ code, activity, status, presenter }: ActivitySlotProps) {
  const polls = useAudiencePolls();
  if (activity?.type === 'poll') {
    if (polls === null)
      throw new Error('A poll card needs the audience connection: render it inside GuestLive.');
    return (
      <section
        data-testid={GUEST_TEST_IDS.activity}
        aria-label="Poll"
        className="flex min-h-0 flex-1 flex-col"
      >
        <PollAnswerLive code={code} activity={activity} status={status} polls={polls}>
          <PollCard activity={activity} presenter={presenter} />
        </PollAnswerLive>
      </section>
    );
  }
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

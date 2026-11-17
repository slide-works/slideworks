import { ActivitySlot } from './ActivitySlot';
import type { AudienceState } from './audienceEvents';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The slide mirror with a progress strip, and the activity card below it.
export function GuestFollowing({ view, state }: { view: GuestView; state: AudienceState }) {
  // TODO: mirror state.current through SlideFrame with the deck's look, the progress strip and the black screen.
  const current = state.current;
  return (
    <main data-testid={GUEST_TEST_IDS.following}>
      <h1 data-testid={GUEST_TEST_IDS.deckTitle}>{view.deckTitle}</h1>
      <p data-testid={GUEST_TEST_IDS.presenter}>{view.presenterName}</p>
      <div data-testid={GUEST_TEST_IDS.slide}>{current?.layout}</div>
      <p data-testid={GUEST_TEST_IDS.progress}>
        {current ? `${current.index} / ${current.count}` : ''}
      </p>
      <ActivitySlot />
    </main>
  );
}

import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The "You're in" state, shown for 2 seconds after the first live state arrives.
export function GuestJoined({ view }: { view: GuestView }) {
  // TODO: the real screen, styled to the mockups.
  return (
    <main data-testid={GUEST_TEST_IDS.joined}>
      <h1>You're in</h1>
      <p>{view.deckTitle}</p>
    </main>
  );
}

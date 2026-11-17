import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// Before Go live: early joiners wait here and the page moves on by itself.
export function GuestLobby({ view }: { view: GuestView }) {
  // TODO: the real screen, styled to the mockups.
  return (
    <main data-testid={GUEST_TEST_IDS.lobby}>
      <h1 data-testid={GUEST_TEST_IDS.deckTitle}>{view.deckTitle}</h1>
      <p>Waiting for {view.presenterName} to start</p>
    </main>
  );
}

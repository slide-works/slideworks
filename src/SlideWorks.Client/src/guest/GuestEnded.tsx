import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The session ended: a thank-you, linking to the slides only when the deck is shared by link.
export function GuestEnded({ view }: { view: GuestView }) {
  // TODO: the real screen, styled to the mockups.
  return (
    <main data-testid={GUEST_TEST_IDS.ended}>
      <h1>Thanks for joining</h1>
      {view.deckUrl && (
        <a data-testid={GUEST_TEST_IDS.endedSlides} href={view.deckUrl}>
          View the slides for {view.deckTitle}
        </a>
      )}
    </main>
  );
}

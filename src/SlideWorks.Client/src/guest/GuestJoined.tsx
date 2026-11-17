import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The "You're in" state, shown for 2 seconds after the first live state arrives. It is a status, not a prompt: nothing
// takes focus, because the page moves on by itself.
export function GuestJoined({ view }: { view: GuestView }) {
  return (
    <main
      data-testid={GUEST_TEST_IDS.joined}
      className="flex flex-1 flex-col items-center justify-center gap-3.5 px-6 text-center"
    >
      <div role="status" className="flex flex-col items-center gap-3.5">
        <div
          aria-hidden
          className="grid size-16 place-items-center rounded-full bg-[rgb(34_197_94/.18)] text-[#86efac]"
        >
          <svg
            viewBox="0 0 24 24"
            className="size-8"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
        </div>
        <h1 className="font-head text-[28px] leading-tight font-bold">You're in</h1>
        <p className="text-base text-(--sw-night-2)">
          {view.deckTitle}
          <br />
          <span className="text-(--sw-night-muted)">with {view.presenterName}</span>
        </p>
        <p className="mt-4 text-sm text-(--sw-night-muted)">
          No app, no account.
          <br />
          This page follows the slides by itself.
        </p>
      </div>
    </main>
  );
}

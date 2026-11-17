import { useEffect, useRef } from 'react';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// Before Go live: early joiners wait here and the page moves on by itself. The deck's logo is not shown, because guest
// images are only served while the session is live.
export function GuestLobby({ view }: { view: GuestView }) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => heading.current?.focus(), []);

  return (
    <main data-testid={GUEST_TEST_IDS.lobby} className="flex flex-1 flex-col gap-4 px-5 pt-5">
      <header className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p
            data-testid={GUEST_TEST_IDS.deckTitle}
            className="truncate font-head text-[15px] font-bold"
          >
            {view.deckTitle}
          </p>
          <p
            data-testid={GUEST_TEST_IDS.presenter}
            className="truncate text-xs text-(--sw-night-muted)"
          >
            {view.presenterName}
          </p>
        </div>
        <span className="shrink-0 rounded-full bg-white/10 px-2.5 py-1 text-xs font-semibold text-(--sw-night-2)">
          Not started
        </span>
      </header>
      <section
        aria-labelledby="guest-lobby-heading"
        className="flex flex-1 flex-col items-center justify-center gap-3 rounded-2xl border border-(--sw-night-line) bg-white/[.04] px-6 py-10 text-center"
      >
        <div aria-hidden className="flex gap-1.5">
          <i className="size-2 rounded-full bg-(--sw-accent-glow)" />
          <i className="size-2 rounded-full bg-(--sw-accent-glow) opacity-70" />
          <i className="size-2 rounded-full bg-(--sw-accent-glow) opacity-40" />
        </div>
        <h1
          id="guest-lobby-heading"
          ref={heading}
          tabIndex={-1}
          className="font-head text-xl font-bold outline-none"
        >
          Starting soon
        </h1>
        <p className="text-sm text-(--sw-night-2)">
          You're in early. This page moves on by itself when {view.presenterName} starts. You can
          lock your phone.
        </p>
      </section>
    </main>
  );
}

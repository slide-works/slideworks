import { useEffect, useRef } from 'react';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The session ended: a thank-you, linking to the slides only when the deck is shared by link. deckUrl is an absolute
// app URL, so it is a plain anchor: the guest page may be on the join host, where a router link would stay on the wrong
// host. The deck's logo is not shown, because guest images are only served while the session is live.
export function GuestEnded({ view }: { view: GuestView }) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => heading.current?.focus(), []);

  return (
    <main
      data-testid={GUEST_TEST_IDS.ended}
      className="flex flex-1 flex-col items-center justify-center gap-3.5 px-6 text-center"
    >
      <div
        aria-hidden
        className="grid size-16 place-items-center rounded-full bg-[rgb(165_184_255/.15)] text-(--sw-accent-glow)"
      >
        <svg
          viewBox="0 0 24 24"
          className="size-8"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.2"
          strokeLinecap="round"
        >
          <path d="M12 21s-7-4.4-9.3-9A5.3 5.3 0 0 1 12 6.4 5.3 5.3 0 0 1 21.3 12C19 16.6 12 21 12 21z" />
        </svg>
      </div>
      <h1
        ref={heading}
        tabIndex={-1}
        className="font-head text-[28px] leading-tight font-bold outline-none"
      >
        Thanks for joining
      </h1>
      <p className="text-base text-(--sw-night-2)">
        {view.deckTitle} has ended.
        <br />
        You can close this page.
      </p>
      {view.deckUrl && (
        <a
          data-testid={GUEST_TEST_IDS.endedSlides}
          href={view.deckUrl}
          aria-label={`Open the slides for ${view.deckTitle}`}
          className="mt-4 flex min-h-12 w-full items-center justify-center rounded-xl bg-white/10 px-4 text-base font-bold text-white no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
        >
          Open the slides
        </a>
      )}
    </main>
  );
}

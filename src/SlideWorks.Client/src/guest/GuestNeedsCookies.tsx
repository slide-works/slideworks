import { useEffect, useRef } from 'react';
import { GUEST_TEST_IDS } from './testIds';

// JoinAudience said no_guest: the browser didn't keep the guest cookie (blocked, or dropped between the read and the
// connection), so the page can't follow the talk. It says why and offers a retry, which reads the view again and so asks
// for the cookie again.
export function GuestNeedsCookies({ onRetry }: { onRetry: () => void }) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => heading.current?.focus(), []);

  return (
    <main
      data-testid={GUEST_TEST_IDS.needsCookies}
      className="flex flex-1 flex-col items-center justify-center gap-3.5 px-6 text-center"
    >
      <h1
        ref={heading}
        tabIndex={-1}
        className="font-head text-[22px] leading-tight font-bold outline-none"
      >
        This page needs cookies
      </h1>
      <p className="text-base text-(--sw-night-2)">
        It keeps a random ID in this browser so you count once and can follow the talk. Allow
        cookies for this site, then try again.
      </p>
      <button
        type="button"
        data-testid={GUEST_TEST_IDS.needsCookiesRetry}
        aria-label="Try again to join the talk"
        onClick={onRetry}
        className="mt-3 min-h-12 w-full rounded-xl bg-white/10 px-4 text-base font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
      >
        Try again
      </button>
    </main>
  );
}

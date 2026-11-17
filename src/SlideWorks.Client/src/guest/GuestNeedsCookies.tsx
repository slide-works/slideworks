import { useEffect, useRef } from 'react';
import { GuestRetryButton } from './GuestRetryButton';
import { GUEST_TEST_IDS } from './testIds';
import type { Retry } from './useRetry';

// JoinAudience said no_guest: the browser didn't keep the guest cookie (blocked, or dropped between the read and the
// connection), so the page can't follow the talk. It says why and offers a retry, which reads the view again and so asks
// for the cookie again.
export function GuestNeedsCookies({ onRetry }: { onRetry: Retry }) {
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
      <GuestRetryButton onRetry={onRetry} testId={GUEST_TEST_IDS.needsCookiesRetry} />
    </main>
  );
}

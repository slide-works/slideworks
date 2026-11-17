import { useEffect, useRef } from 'react';
import { GUEST_TEST_IDS } from './testIds';

// How often the offline screen tries again by itself.
export const OFFLINE_RETRY_MS = 5000;

// Live updates can't reach the phone (every transport failed, or it is offline). It explains and retries: every few
// seconds on its own while it is shown, and at once on "Try now". It sits on the shell's night surface.
export function GuestOffline({ onRetry }: { onRetry: () => void }) {
  const heading = useRef<HTMLHeadingElement>(null);
  const retry = useRef(onRetry);
  useEffect(() => {
    retry.current = onRetry;
  });
  useEffect(() => heading.current?.focus(), []);
  useEffect(() => {
    const timer = setInterval(() => retry.current(), OFFLINE_RETRY_MS);
    return () => clearInterval(timer);
  }, []);

  return (
    <main
      data-testid={GUEST_TEST_IDS.offline}
      className="flex flex-1 flex-col items-center justify-center gap-3.5 px-6 text-center"
    >
      <div
        aria-hidden
        className="grid size-16 place-items-center rounded-full bg-[rgb(234_88_12/.18)] text-[#fdba74]"
      >
        <svg
          viewBox="0 0 24 24"
          className="size-8"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.2"
          strokeLinecap="round"
        >
          <path d="M2 8.5a15 15 0 0 1 20 0M5 12a10 10 0 0 1 14 0M8.5 15.5a5 5 0 0 1 7 0" />
          <circle cx="12" cy="19" r="1" fill="currentColor" />
          <path d="M3 3l18 18" />
        </svg>
      </div>
      <h1
        ref={heading}
        tabIndex={-1}
        className="font-head text-[22px] leading-tight font-bold outline-none"
      >
        Can't reach the room
      </h1>
      <p className="text-base text-(--sw-night-2)">
        This network may block live updates, or you're offline. We'll keep trying every few seconds.
      </p>
      <button
        type="button"
        data-testid={GUEST_TEST_IDS.offlineRetry}
        aria-label="Try now to reach the room"
        onClick={onRetry}
        className="mt-3 min-h-12 w-full rounded-xl bg-white/10 px-4 text-base font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
      >
        Try now
      </button>
    </main>
  );
}

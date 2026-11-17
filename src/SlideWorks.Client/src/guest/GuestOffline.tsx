import { useEffect, useRef } from 'react';
import { GUEST_TEST_IDS } from './testIds';
import type { Retry } from './useRetry';

// The offline screen's own retries. A room that drops together comes back to this screen together, and a venue shares one
// rate limit, so each phone waits a base time plus a random extra, and the base doubles after every attempt up to a cap.
export const OFFLINE_RETRY_MS = 5000;
export const OFFLINE_JITTER_MS = 2000;
export const OFFLINE_MAX_RETRY_MS = 30000;

// Live updates can't reach the phone (every transport failed, or it is offline). It explains and retries on its own while
// it is shown: the next attempt is scheduled only after the last one has settled (onRetry returns a promise for that),
// after a wait that doubles from 5 s to 30 s with a random extra of up to 2 s. "Try now" retries at once and starts the
// waits again. It sits on the shell's night surface. random is injected so tests are deterministic.
export function GuestOffline({
  onRetry,
  random = Math.random,
}: {
  onRetry: Retry;
  random?: () => number;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  const retry = useRef(onRetry);
  const randomRef = useRef(random);
  const tryNow = useRef<() => void>(() => undefined);
  useEffect(() => {
    retry.current = onRetry;
    randomRef.current = random;
  });
  useEffect(() => heading.current?.focus(), []);
  useEffect(() => {
    let active = true;
    let inFlight = false;
    let delay = OFFLINE_RETRY_MS;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const schedule = () => {
      clearTimeout(timer);
      timer = setTimeout(attempt, delay + Math.floor(randomRef.current() * OFFLINE_JITTER_MS));
    };
    function attempt() {
      inFlight = true;
      clearTimeout(timer);
      void Promise.resolve()
        .then(() => retry.current())
        .catch(() => undefined)
        .then(() => {
          inFlight = false;
          if (!active) return;
          delay = Math.min(delay * 2, OFFLINE_MAX_RETRY_MS);
          schedule();
        });
    }
    tryNow.current = () => {
      // The wait after this attempt is the base again (the doubling after an attempt takes it back up from half of it).
      delay = OFFLINE_RETRY_MS / 2;
      // An attempt already running reschedules itself when it settles.
      if (!inFlight) attempt();
    };
    schedule();
    return () => {
      active = false;
      clearTimeout(timer);
    };
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
        onClick={() => tryNow.current()}
        className="mt-3 min-h-12 w-full rounded-xl bg-white/10 px-4 text-base font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
      >
        Try now
      </button>
    </main>
  );
}

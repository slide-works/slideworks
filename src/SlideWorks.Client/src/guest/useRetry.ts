import { useCallback, useEffect, useRef, useState } from 'react';
import { SLOW_JITTER_MS, THROTTLED_RETRY_MS } from './useAudience';

// What a retry reports: 'throttled' when the venue's rate limit refused the read (so nothing changed and the screen should
// say so and try again later), anything else when the retry went through.
export type RetryOutcome = 'done' | 'throttled';
// A retry may return a promise of its outcome; anything but 'throttled' counts as gone through.
export type Retry = () => unknown;

// A retry button's behaviour: pending while the call is in flight, and after a throttled answer a short message and an
// automatic retry after the same 5 to 10 s jittered wait that useAudience uses for a rate limit. random is injected so
// tests are deterministic.
export function useRetry(onRetry: Retry, random: () => number = Math.random) {
  const [pending, setPending] = useState(false);
  const [throttled, setThrottled] = useState(false);
  const retry = useRef(onRetry);
  const randomRef = useRef(random);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const mounted = useRef(true);
  const running = useRef(false);
  const again = useRef<() => void>(() => undefined);
  useEffect(() => {
    retry.current = onRetry;
    randomRef.current = random;
  });
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      clearTimeout(timer.current);
    };
  }, []);

  const run = useCallback(() => {
    if (running.current) return;
    running.current = true;
    clearTimeout(timer.current);
    setPending(true);
    void Promise.resolve()
      .then(() => retry.current())
      .catch(() => undefined)
      .then((outcome) => {
        running.current = false;
        if (!mounted.current) return;
        setPending(false);
        const wasThrottled = outcome === 'throttled';
        setThrottled(wasThrottled);
        if (wasThrottled)
          timer.current = setTimeout(
            () => again.current(),
            THROTTLED_RETRY_MS + Math.floor(randomRef.current() * SLOW_JITTER_MS),
          );
      });
  }, []);
  useEffect(() => {
    again.current = run;
  }, [run]);

  return { pending, throttled, run };
}

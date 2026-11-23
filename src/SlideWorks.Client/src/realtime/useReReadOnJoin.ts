import { useCallback, useEffect, useRef, type MutableRefObject } from 'react';

/**
 * The first read of a page's deck and the re-read once the deck is joined.
 * `read` starts a read and returns when it settles; `latest` is the page's
 * latest-wins counter, which `read` bumps for every read it starts.
 * Returns the callback to pass to useDeckRealtime as its join handler.
 */
export function useReReadOnJoin(
  read: () => Promise<void>,
  latest: MutableRefObject<number>,
): () => void {
  // The first read of the current deck. A join waits for it before re-reading,
  // so the first load is never thrown away for a re-read.
  const firstRead = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    firstRead.current = read();
    // Leaving this deck (or the page) makes every read still in flight stale,
    // and drops a join still waiting on this first read, so an unmounted page
    // sends no re-read.
    return () => {
      latest.current += 1;
      firstRead.current = Promise.resolve();
    };
  }, [read, latest]);

  // Re-read once the deck is joined, and again after a reconnect re-joins it:
  // events are only delivered from the join on, so a change that landed
  // between the first read and the join (or while offline) shows up here.
  return useCallback(() => {
    const first = firstRead.current;
    void first.then(() => {
      // Still the same deck: a join from before a move to another is dropped.
      if (firstRead.current === first) void read();
    });
  }, [read]);
}

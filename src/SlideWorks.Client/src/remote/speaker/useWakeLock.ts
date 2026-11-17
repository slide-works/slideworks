import { useEffect } from 'react';

// Keeps the phone's screen awake while active (P8: while the presentation is live) and lets it go otherwise. The
// browser drops a screen wake lock whenever the page is hidden, so it is asked for again when the page comes back.
// Where the API is missing, or the browser says no (low battery, no user activation), the screen just sleeps as usual.
export function useWakeLock(active: boolean) {
  useEffect(() => {
    if (!active) return;
    let gone = false;
    let sentinel: WakeLockSentinel | null = null;

    const request = async () => {
      if (document.visibilityState !== 'visible' || sentinel !== null) return;
      try {
        const lock = await navigator.wakeLock.request('screen');
        if (gone) {
          await lock.release();
          return;
        }
        sentinel = lock;
        lock.addEventListener('release', () => {
          if (sentinel === lock) sentinel = null;
        });
      } catch {
        // Unsupported (navigator.wakeLock is undefined) or refused: nothing to hold.
      }
    };
    const onVisible = () => void request();

    void request();
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      gone = true;
      document.removeEventListener('visibilitychange', onVisible);
      const held = sentinel;
      sentinel = null;
      if (held) void held.release().catch(() => undefined);
    };
  }, [active]);
}

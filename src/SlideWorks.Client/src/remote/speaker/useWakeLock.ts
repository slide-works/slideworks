import { useEffect } from 'react';

// Keeps the phone's screen awake while active (P8: while the presentation is live) and lets it go otherwise. The
// browser drops a screen wake lock whenever the page is hidden, so it is asked for again when the page comes back.
// Where the API is missing, or the browser says no (low battery, no user activation), the screen just sleeps as usual.
export function useWakeLock(active: boolean) {
  useEffect(() => {
    if (!active) return;
    let gone = false;
    let sentinel: WakeLockSentinel | null = null;
    // A request in flight: a visibility change meanwhile must not start a second one, so only one lock is ever held.
    let pending = false;

    const request = async () => {
      if (document.visibilityState !== 'visible' || sentinel !== null || pending) return;
      pending = true;
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
      } finally {
        pending = false;
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

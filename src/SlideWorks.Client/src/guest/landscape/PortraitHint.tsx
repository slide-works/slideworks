import { useEffect, useState, type ReactNode } from 'react';
import { GUEST_TEST_IDS } from '../testIds';

const STORAGE_KEY = 'sw.landscapeHint';
const LANDSCAPE_QUERY = '(orientation: landscape)';

function alreadySeen(): boolean {
  try {
    return window.localStorage.getItem(STORAGE_KEY) !== null;
  } catch {
    return false;
  }
}

function remember(): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, '1');
  } catch {
    // Storage is a per-viewer convenience; without it the hint may show again.
  }
}

// The one-time "Turn sideways to read the slide full screen" hint above the slide in portrait (SW-883 D6). It goes away
// with its close button or once the phone has been turned, and stays away: the choice is kept in localStorage.
export function PortraitHint(): ReactNode {
  const [dismissed, setDismissed] = useState(alreadySeen);

  useEffect(() => {
    const query = window.matchMedia(LANDSCAPE_QUERY);
    const turned = () => {
      remember();
      setDismissed(true);
    };
    const onChange = (event: MediaQueryListEvent) => {
      if (event.matches) turned();
    };
    if (query.matches) turned();
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, []);

  if (dismissed) return null;
  return (
    <div
      data-testid={GUEST_TEST_IDS.portraitHint}
      className="flex items-center gap-2 rounded-[10px] bg-white/10 px-3 py-2 text-xs text-(--sw-night-2) [@media(orientation:landscape)]:hidden"
    >
      <p className="flex-1">Turn sideways to read the slide full screen</p>
      <button
        type="button"
        aria-label="Close hint"
        data-testid={GUEST_TEST_IDS.portraitHintClose}
        onClick={() => {
          remember();
          setDismissed(true);
        }}
        className="rounded-full px-2 py-1"
      >
        <span aria-hidden>×</span>
      </button>
    </div>
  );
}

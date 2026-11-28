import { useEffect, useRef, useState, type ReactNode } from 'react';
import { GUEST_TEST_IDS } from '../testIds';

const STORAGE_KEY = 'sw.landscapeHint';
const LANDSCAPE_QUERY = '(orientation: landscape)';
const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select, textarea, [tabindex]:not([tabindex="-1"])';

// Closing the hint removes the focused button, so focus goes to the next focusable element after the hint, or to the
// hint's parent when nothing usable follows it. Anything inside an inert subtree (the slide mirror) cannot take focus
// and is skipped.
function moveFocusPast(hint: HTMLElement): void {
  const next = Array.from(document.querySelectorAll<HTMLElement>(FOCUSABLE)).find(
    (element) =>
      !hint.contains(element) &&
      (hint.compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0 &&
      element.closest('[inert]') === null &&
      element.getClientRects().length > 0,
  );
  if (next !== undefined) {
    next.focus();
    if (document.activeElement === next) return;
  }
  const parent = hint.parentElement;
  if (parent === null) return;
  parent.setAttribute('tabindex', '-1');
  parent.focus();
}

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
// with its close button or once the phone has been turned, and only on a phone in portrait, and stays away: the choice is kept in localStorage.
export function PortraitHint(): ReactNode {
  const [dismissed, setDismissed] = useState(alreadySeen);
  const hint = useRef<HTMLDivElement | null>(null);

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
      ref={hint}
      data-testid={GUEST_TEST_IDS.portraitHint}
      className="hidden items-center gap-2 rounded-[10px] bg-white/10 px-3 py-2 text-xs text-(--sw-night-2) [@media(orientation:portrait)_and_(max-width:500px)]:flex"
    >
      <p className="flex-1">Turn sideways to read the slide full screen</p>
      <button
        type="button"
        aria-label="Close hint"
        data-testid={GUEST_TEST_IDS.portraitHintClose}
        onClick={() => {
          if (hint.current !== null) moveFocusPast(hint.current);
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

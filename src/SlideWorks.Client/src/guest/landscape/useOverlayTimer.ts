import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';

export const OVERLAY_VISIBLE_MS = 3000;

// The landscape overlay's visibility: a tap shows it for 3 seconds and a tap while it shows hides it. Timers are plain
// setTimeout, so a test drives them with page.clock. The timer never hides the overlay while keyboard focus is inside
// heldBy (the overlay element): the layer calls keepAlive when focus leaves, which re-arms it.
export function useOverlayTimer(heldBy: RefObject<HTMLElement | null>): {
  visible: boolean;
  toggle: () => void;
  hide: () => void;
  keepAlive: () => void;
} {
  const [visible, setVisible] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clear = useCallback(() => {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }, []);

  const arm = useCallback(() => {
    clear();
    timer.current = setTimeout(() => {
      timer.current = null;
      const held = heldBy.current;
      const active = document.activeElement;
      // Only keyboard focus holds the overlay: after a pointer press the focus stays on the button but nobody is
      // using it.
      if (held !== null && held.contains(active) && active?.matches(':focus-visible') === true)
        return;
      setVisible(false);
    }, OVERLAY_VISIBLE_MS);
  }, [clear, heldBy]);

  const hide = useCallback(() => {
    clear();
    setVisible(false);
  }, [clear]);

  const toggle = useCallback(() => {
    if (visible) {
      hide();
    } else {
      setVisible(true);
      arm();
    }
  }, [visible, arm, hide]);

  useEffect(() => clear, [clear]);

  return { visible, toggle, hide, keepAlive: arm };
}

import { useCallback, useEffect, useRef, useState } from 'react';

export const OVERLAY_VISIBLE_MS = 3000;

// The landscape overlay's visibility: a tap shows it for 3 seconds and a tap while it shows hides it. Timers are plain
// setTimeout, so a test drives them with page.clock.
export function useOverlayTimer(): { visible: boolean; toggle: () => void; keepAlive: () => void } {
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
      setVisible(false);
    }, OVERLAY_VISIBLE_MS);
  }, [clear]);

  const toggle = useCallback(() => {
    if (visible) {
      clear();
      setVisible(false);
    } else {
      setVisible(true);
      arm();
    }
  }, [visible, arm, clear]);

  useEffect(() => clear, [clear]);

  return { visible, toggle, keepAlive: arm };
}

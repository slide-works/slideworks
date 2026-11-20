import { useLayoutEffect, useRef, useState } from 'react';

// After a control action, focus stays on the control that was used. When that control has gone (Close gives way to
// New round), focus stays in the panel instead of dropping to the page.
export function useKeptFocus() {
  const root = useRef<HTMLElement>(null);
  const restore = useRef<HTMLElement | null>(null);
  const sending = useRef(false);
  const [busy, setBusy] = useState(false);
  const [tick, setTick] = useState(0);

  useLayoutEffect(() => {
    const used = restore.current;
    if (used === null) return;
    restore.current = null;
    if (used.isConnected) {
      if (document.activeElement !== used) used.focus();
      return;
    }
    const panel = root.current;
    if (panel !== null && !panel.contains(document.activeElement)) panel.focus();
  }, [tick]);

  const run = async (send: () => Promise<void>, used: HTMLElement | null) => {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    try {
      await send();
    } finally {
      sending.current = false;
      setBusy(false);
      restore.current = used;
      setTick((n) => n + 1);
    }
  };

  return { root, busy, run };
}

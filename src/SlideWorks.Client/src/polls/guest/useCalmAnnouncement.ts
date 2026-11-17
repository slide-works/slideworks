import { useEffect, useRef, useState } from 'react';

// The text a polite live region should hold for a value that may change many times a second: the first change is
// announced at once, then at most one change per quiet period, and the last value always lands. Timers only, no clock.
export function useCalmAnnouncement(text: string, quietMs = 5000): string {
  const [said, setSaid] = useState(text);
  const saidNow = useRef(text);
  const latest = useRef(text);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    latest.current = text;
    if (timer.current !== null) return;
    const flush = () => {
      timer.current = null;
      if (latest.current === saidNow.current) return;
      saidNow.current = latest.current;
      setSaid(latest.current);
      timer.current = setTimeout(flush, quietMs);
    };
    timer.current = setTimeout(flush, 0);
  }, [text, quietMs]);

  useEffect(
    () => () => {
      if (timer.current !== null) clearTimeout(timer.current);
      timer.current = null;
    },
    [],
  );

  return said;
}

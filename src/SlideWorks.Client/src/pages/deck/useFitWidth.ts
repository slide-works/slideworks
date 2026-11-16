import { useLayoutEffect, useRef, useState } from 'react';

// The scale that fits content `contentWidth` px wide into the measured width of
// the returned element, updated as the element resizes.
export function useFitWidth<T extends HTMLElement>(contentWidth: number) {
  const ref = useRef<T>(null);
  const [scale, setScale] = useState(0);

  useLayoutEffect(() => {
    const element = ref.current;
    if (element === null) return;
    const measure = () => setScale(element.clientWidth / contentWidth);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [contentWidth]);

  return { ref, scale };
}

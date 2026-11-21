import { useEffect, useRef, useState, type ReactNode } from 'react';
import { measureSlide } from '../checks/measureSlide';

// Draws a rendered slide at 1920x1080, waits for fonts and images, runs measureSlide on its frame and publishes the
// findings as JSON on data-findings, so a component test can read plain data.
export function MeasureProbe({ slide, children }: { slide: number; children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const [findings, setFindings] = useState<string>();
  useEffect(() => {
    const root = ref.current!;
    void (async () => {
      await document.fonts.ready;
      const images = Array.from(root.querySelectorAll('img'));
      await Promise.all(images.map((img) => img.decode().catch(() => undefined)));
      const frame = root.querySelector<HTMLElement>('[data-testid="slide-frame"]')!;
      setFindings(JSON.stringify(measureSlide(frame, slide)));
    })();
  }, [slide]);
  return (
    <div ref={ref} style={{ width: 1920, height: 1080 }} data-findings={findings}>
      {children}
    </div>
  );
}

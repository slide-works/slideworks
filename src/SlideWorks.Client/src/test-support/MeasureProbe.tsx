import { useEffect, useRef, useState, type ReactNode } from 'react';
import { measureSlide } from '../checks/measureSlide';
import { fontsSettled } from '../look/fontsSettled';

// Draws a rendered slide at 1920x1080, waits for the look's fonts, images and two animation frames, runs measureSlide on its frame and publishes the
// findings as JSON on data-findings, so a component test can read plain data.
export function MeasureProbe({
  slide,
  look,
  children,
}: {
  slide: number;
  look?: Parameters<typeof fontsSettled>[0];
  children: ReactNode;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [findings, setFindings] = useState<string>();
  useEffect(() => {
    const root = ref.current!;
    void (async () => {
      await fontsSettled(look);
      const images = Array.from(root.querySelectorAll('img'));
      await Promise.all(images.map((img) => img.decode().catch(() => undefined)));
      // Two frames let the layout that the loaded fonts and images change settle before measuring.
      for (let i = 0; i < 2; i++) await new Promise((r) => requestAnimationFrame(r));
      const frame = root.querySelector<HTMLElement>('[data-testid="slide-frame"]')!;
      setFindings(JSON.stringify(measureSlide(frame, slide)));
    })();
  }, [slide, look]);
  return (
    <div ref={ref} style={{ width: 1920, height: 1080 }} data-findings={findings}>
      {children}
    </div>
  );
}

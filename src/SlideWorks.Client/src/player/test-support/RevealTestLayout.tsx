import type { LayoutProps } from '../../slides/slots';
import { Reveal } from '../../slides/Reveal';

// Test only: a bullets layout built on the real Reveal, standing in for the
// stub until the real layouts land, so player tests can see what a stepped
// slide does on a hidden layer. Each item holds a button, so it can take focus.
export function RevealTestLayout({ step }: LayoutProps<'bullets'>) {
  return (
    <ul data-testid="reveal-layout" className="flex flex-col gap-4 p-24 text-5xl">
      {[1, 2, 3, 4].map((at) => (
        <Reveal key={at} at={at} step={step} as="li">
          <button type="button" data-testid={`reveal-item-${at}`}>
            Point {at}
          </button>
        </Reveal>
      ))}
    </ul>
  );
}

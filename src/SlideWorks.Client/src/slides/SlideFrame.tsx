import type { ReactNode } from 'react';
import type { LayoutName } from './slots';

export type SlideFrameProps = {
  layout: LayoutName;
  dark: boolean;
  // The slide's part (1-based), or null when it belongs to none.
  part: number | null;
  parts: string[];
  number: number;
  total: number;
  deckTitle: string;
  author: string | null;
  children: ReactNode;
};

// The theme chrome around a slide: part tracker, footer and progress bar (D5).
// TODO: stub that draws only the surface; the real theme chrome replaces it.
export function SlideFrame({ layout, dark, children }: SlideFrameProps) {
  return (
    <div
      data-testid="slide-frame"
      data-layout={layout}
      className="h-full w-full"
      style={{
        boxSizing: 'border-box',
        padding: 'var(--sw-slide-pad-top) var(--sw-slide-pad-x) var(--sw-slide-pad-bottom)',
        background: dark ? 'var(--sw-night-surface)' : 'var(--sw-paper)',
        color: dark ? 'var(--sw-night-ink)' : 'var(--sw-ink)',
        fontFamily: 'var(--sw-font-body)',
      }}
    >
      {children}
    </div>
  );
}

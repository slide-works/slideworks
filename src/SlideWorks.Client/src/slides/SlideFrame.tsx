import type { ReactNode } from 'react';
import { PartTracker } from './chrome/PartTracker';
import { ProgressBar } from './chrome/ProgressBar';
import { SlideFooter } from './chrome/SlideFooter';
import './chrome/chrome.css';
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

// The default theme's frame around a slide (D4, D5): a light surface for content
// slides and a dark one for title, divider and close, the accent strip on top,
// the part tracker and footer on content slides, and the progress bar on every
// slide. Dark slides carry no tracker (the divider shows the journey instead)
// and no footer. The frame is a flex column inside the theme's padding, so a
// layout's body can take the remaining height with flex: 1.
export function SlideFrame({
  layout,
  dark,
  part,
  parts,
  number,
  total,
  deckTitle,
  author,
  children,
}: SlideFrameProps) {
  return (
    <div
      className="sw-frame"
      data-testid="slide-frame"
      data-layout={layout}
      data-dark={dark ? 'true' : 'false'}
    >
      {!dark && parts.length > 0 ? <PartTracker parts={parts} part={part} /> : null}
      {children}
      {dark ? null : (
        <SlideFooter number={number} total={total} deckTitle={deckTitle} author={author} />
      )}
      <ProgressBar number={number} total={total} />
    </div>
  );
}

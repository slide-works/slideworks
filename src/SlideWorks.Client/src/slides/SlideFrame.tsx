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
// and no footer. The frame is a flex column inside the theme's padding, and
// chrome.css defines the seam every layout uses:
// - `sw-layout` goes on the layout's root element, the frame's flex item. It
//   fills the content box, x 120–1800 and y 150–960, as a flex column.
// - `sw-body` goes on a content layout's body, under the kicker and headline.
//   It takes the remaining height, 46 px below the header, and is not clipped,
//   so its content may run to y = 996, above the footer text at y = 1023.
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

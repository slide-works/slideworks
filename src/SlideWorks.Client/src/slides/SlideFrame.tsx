import type { ReactNode } from 'react';
import { DeepDiveMark } from './chrome/DeepDiveMark';
import { DraftStamp } from './chrome/DraftStamp';
import { JoinBar } from './chrome/JoinBar';
import { PartTracker } from './chrome/PartTracker';
import { ProgressBar } from './chrome/ProgressBar';
import { SlideFooter } from './chrome/SlideFooter';
import './chrome/chrome.css';
import type { LookView } from '../api/types';
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
  // The deck's look, for the template's chrome, logo and draft mark.
  look: LookView;
  // The slide is flagged deep_dive.
  deepDive: boolean;
  // The title slot's own stamp on a title slide, else null.
  titleStamp: string | null;
  children: ReactNode;
};

// The deck's frame around a slide (D4, D5; SW-2 mockup sections 04 and 05):
// a light surface for content slides and a dark one for title, divider and
// close, the top strip, then the template's chrome after the deck's
// overrides (look.template.chrome):
// - the part tracker on content slides, when the chrome has it;
// - the footer on content slides, with the author or the server's date label,
//   the draft chip and the deck's logo, unless the chrome turns it off;
// - the progress bar on every slide, when the chrome has it;
// - the draft stamp on the title slide of a draft deck, unless the title slot
//   has its own stamp, which the title layout draws instead;
// - on a deep dive, the top strip in the deep-dive colour and the pill.
// Dark slides carry no tracker (the divider shows the journey instead) and no
// footer. The frame is a flex column inside the theme's padding, and
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
  look,
  deepDive,
  titleStamp,
  children,
}: SlideFrameProps) {
  const { chrome } = look.template;
  const meta = chrome.footer === 'date' ? look.footerDateLabel : author;
  return (
    <div
      className="sw-frame"
      data-testid="slide-frame"
      data-layout={layout}
      data-dark={dark ? 'true' : 'false'}
      data-deep-dive={deepDive ? 'true' : 'false'}
    >
      {!dark && chrome.tracker && parts.length > 0 ? (
        <PartTracker parts={parts} part={part} />
      ) : null}
      {deepDive ? <DeepDiveMark /> : null}
      {children}
      {layout === 'title' && look.draft && titleStamp === null ? <DraftStamp /> : null}
      {dark || chrome.footer === 'off' ? null : (
        <SlideFooter
          number={number}
          total={total}
          deckTitle={deckTitle}
          meta={meta}
          logoUrl={look.logo?.url ?? null}
          draft={look.draft}
        />
      )}
      {look.joinBar && layout !== 'join' ? <JoinBar /> : null}
      {chrome.progress ? <ProgressBar number={number} total={total} /> : null}
    </div>
  );
}

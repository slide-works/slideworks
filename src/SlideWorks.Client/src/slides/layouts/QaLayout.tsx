import { useQaScreen } from '../../qa/screen/QaScreenContext';
import { QA_SLIDE_TEST_IDS } from '../../qa/testIds';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { DEFAULT_QA_TITLE } from './qaTitle';

// The Q&A slide (light, SW-9 Q1, Q9): its title over the room's questions, from useQaScreen(). With no live data (the
// deck page, RenderPage, preview_deck, a viewer who can't present, a deck that isn't live) it draws the "not live" state.
// TODO: draws only the title and an empty not-live state; the real Q&A slide (the list, the question on screen, the
// empty, closed and not-live states, the join pill) replaces it.
export function QaLayout({ slots }: LayoutProps<'qa'>) {
  const screen = useQaScreen();
  return (
    <div data-testid="layout-qa" className="sw-layout">
      <div data-testid={QA_SLIDE_TEST_IDS.slide} className="contents">
        <SlideHeading layout="qa" headline={slots.title ?? DEFAULT_QA_TITLE} />
        <div className="sw-body">
          {screen === null ? <div data-testid={QA_SLIDE_TEST_IDS.notLive} /> : null}
        </div>
      </div>
    </div>
  );
}

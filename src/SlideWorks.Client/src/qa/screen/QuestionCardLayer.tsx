import { QA_CARD_TEST_IDS } from '../testIds';
import { useQaScreen } from './QaScreenContext';

// The question card over any slide (SW-9 Q9), mounted once over the player's canvas. onQaSlide: the slide on screen is
// a qa slide, which draws the shown question itself. With no live data (useQaScreen() null) it renders nothing, so the
// deck page, RenderPage and thumbnails are unchanged.
// TODO: draws only an empty layer; the real card (shown when a question is on screen, showOn is anySlide and no qa slide
// is up) replaces it.
export function QuestionCardLayer({ onQaSlide }: { onQaSlide: boolean }) {
  const screen = useQaScreen();
  if (screen === null) return null;
  return (
    <div
      data-testid={QA_CARD_TEST_IDS.layer}
      data-qa-slide={onQaSlide}
      className="pointer-events-none absolute inset-0"
    />
  );
}

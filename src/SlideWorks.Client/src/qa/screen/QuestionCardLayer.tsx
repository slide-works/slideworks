import { QA_CARD_TEST_IDS } from '../testIds';
import { useQaScreen } from './QaScreenContext';
import styles from './QuestionCardLayer.module.css';

// The question card over any slide (SW-9 Q9), mounted once over the player's canvas. onQaSlide: the slide on screen is
// a qa slide, which draws the shown question itself. With no live data (useQaScreen() null) it renders nothing, so the
// deck page, RenderPage and thumbnails are unchanged. The card shows when a question is on screen, showOn is anySlide
// and no qa slide is up; the slide dims behind it. The card is dark (the ink token) with the name at the top right, as the mockup draws it;
// the text and the name are dir="auto" so a right-to-left question reads right.
export function QuestionCardLayer({ onQaSlide }: { onQaSlide: boolean }) {
  const screen = useQaScreen();
  if (screen === null) return null;
  const question = screen.onScreen;
  const shown = question !== null && screen.showOn === 'anySlide' && !onQaSlide;
  return (
    <div
      data-testid={QA_CARD_TEST_IDS.layer}
      data-qa-slide={onQaSlide}
      className="pointer-events-none absolute inset-0"
    >
      {shown && (
        <>
          <div data-testid={QA_CARD_TEST_IDS.dim} className={styles.dim} aria-hidden="true" />
          <section data-testid={QA_CARD_TEST_IDS.card} className={styles.card}>
            <p className={styles.head}>
              <span data-testid={QA_CARD_TEST_IDS.label}>Question from the room</span>
              <span data-testid={QA_CARD_TEST_IDS.votes} className={styles.votes}>
                {`▲ ${question.votes}`}
              </span>
              <span data-testid={QA_CARD_TEST_IDS.name} dir="auto" className={styles.name}>
                {question.name ?? 'Anonymous'}
              </span>
            </p>
            <p data-testid={QA_CARD_TEST_IDS.text} dir="auto" className={styles.text}>
              {question.text}
            </p>
          </section>
        </>
      )}
    </div>
  );
}

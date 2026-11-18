import { QA_SETTINGS_CARD_TEST_IDS as IDS } from '../testIds';
import type { QaSettings } from '../types';
import { QaSlideSwitch } from './QaSlideSwitch';
import { useChangeError } from './useChangeError';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
  // The slide selected on the deck page, 1-based, whose switch the card shows; null when the deck has no slides.
  slide: number | null;
};

const QUESTIONS: ReadonlyArray<{ value: QaSettings['questions']; label: string; id: string }> = [
  { value: 'open', label: 'Open', id: IDS.questionsOpen },
  { value: 'closed', label: 'Closed', id: IDS.questionsClosed },
  { value: 'off', label: 'Off', id: IDS.questionsOff },
];

const SHOW_ON: ReadonlyArray<{ value: QaSettings['showOn']; label: string; id: string }> = [
  { value: 'anySlide', label: 'over any slide', id: IDS.showOnAnySlide },
  { value: 'qaSlide', label: 'only on the Q&A slide', id: IDS.showOnQaSlide },
];

// The deck page's "Q&A and reactions" card (SW-9 Q9), for a person who can present the deck, under QaControlLive: the
// deck's five settings and the selected slide's switch, live or not. Each change is one setSettings or setSlide call
// with just the changed field; the controls read the view, so a change that fails goes back by itself and its message
// shows in the card. A slide not in the view's `slides` has both switches on.
export function QaSettingsCard({ deckId, slide }: Props) {
  const { control, setSettings, setSlide, error } = useQaControl(deckId);
  const { message: changeMessage, run } = useChangeError();
  const settings = control?.settings ?? null;
  // Before the first read lands the only message is the provider's own (a failed read), not another part's change.
  const message = control === null ? (error ?? changeMessage) : changeMessage;
  const switches =
    slide === null
      ? null
      : (control?.slides.find((entry) => entry.slide === slide) ?? {
          slide,
          questions: true,
          reactions: true,
        });
  return (
    <section
      data-testid={IDS.card}
      aria-label="Q&A and reactions"
      className="panel grid gap-3 p-4 text-meta"
    >
      <h2 data-testid={IDS.title} className="text-base font-semibold text-ink">
        Q&amp;A and reactions
      </h2>
      {settings === null ? (
        <p className="text-ink-muted">
          {error === null ? 'Loading the settings.' : 'The settings could not be loaded.'}
        </p>
      ) : (
        <>
          <div
            role="radiogroup"
            aria-label="Questions"
            data-testid={IDS.questions}
            className="grid gap-1"
          >
            <span className="font-semibold text-ink-muted">Questions</span>
            <span className="flex gap-3">
              {QUESTIONS.map(({ value, label, id }) => (
                <label key={value} className="inline-flex items-center gap-1.5">
                  <input
                    data-testid={id}
                    type="radio"
                    name={`qa-questions-${deckId}`}
                    checked={settings.questions === value}
                    onChange={() => void run(() => setSettings({ questions: value }))}
                  />
                  {label}
                </label>
              ))}
            </span>
          </div>
          <label className="inline-flex items-center gap-1.5">
            <input
              data-testid={IDS.review}
              type="checkbox"
              checked={settings.review}
              onChange={(event) => void run(() => setSettings({ review: event.target.checked }))}
            />
            Review questions first
          </label>
          <label className="inline-flex items-center gap-1.5">
            <input
              data-testid={IDS.names}
              type="checkbox"
              checked={settings.names}
              onChange={(event) => void run(() => setSettings({ names: event.target.checked }))}
            />
            Names allowed
          </label>
          <label className="inline-flex items-center gap-1.5">
            <input
              data-testid={IDS.reactions}
              type="checkbox"
              checked={settings.reactions}
              onChange={(event) => void run(() => setSettings({ reactions: event.target.checked }))}
            />
            Reactions
          </label>
          <div
            role="radiogroup"
            aria-label="Shown questions appear"
            data-testid={IDS.showOn}
            className="grid gap-1"
          >
            <span className="font-semibold text-ink-muted">Shown questions appear</span>
            <span className="flex flex-wrap gap-x-3">
              {SHOW_ON.map(({ value, label, id }) => (
                <label key={value} className="inline-flex items-center gap-1.5">
                  <input
                    data-testid={id}
                    type="radio"
                    name={`qa-show-on-${deckId}`}
                    checked={settings.showOn === value}
                    onChange={() => void run(() => setSettings({ showOn: value }))}
                  />
                  {label}
                </label>
              ))}
            </span>
          </div>
          {switches !== null && (
            <>
              <QaSlideSwitch
                label={`Slide ${switches.slide}: questions`}
                on={switches.questions}
                reason={settings.questions === 'off' ? 'Questions are off for the deck' : null}
                onChange={(on) => void run(() => setSlide(switches.slide, { questions: on }))}
                testId={IDS.slideQuestions}
                reasonTestId={IDS.slideQuestionsReason}
                labelClass="text-ink"
                reasonClass="text-ink-muted"
              />
              <QaSlideSwitch
                label={`Slide ${switches.slide}: reactions`}
                on={switches.reactions}
                reason={settings.reactions ? null : 'Reactions are off for the deck'}
                onChange={(on) => void run(() => setSlide(switches.slide, { reactions: on }))}
                testId={IDS.slideReactions}
                reasonTestId={IDS.slideReactionsReason}
                labelClass="text-ink"
                reasonClass="text-ink-muted"
              />
            </>
          )}
        </>
      )}
      {message !== null && (
        <p role="alert" data-testid={IDS.error} className="text-bad">
          {message}
        </p>
      )}
    </section>
  );
}

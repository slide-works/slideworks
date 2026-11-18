import { QA_SETTINGS_STRIP_TEST_IDS as IDS } from '../testIds';
import { QaSlideSwitch } from './QaSlideSwitch';
import { useChangeError } from './useChangeError';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
};

const TOGGLE =
  'min-h-7 rounded-[7px] border border-[#334155] px-2 py-1 text-[11.5px] leading-none text-[#e2e8f0] aria-pressed:border-[#8ea5ff] aria-pressed:bg-[#8ea5ff]/15 disabled:opacity-50';

// The settings strip in the Questions panel (SW-9 Q9): the deck's questions, review, names and reactions toggles, and
// the switch of the slide the presenter is on (the view's `here`), each with its reason when the deck has that feature
// off. Each change is one setSettings or setSlide call with just the changed field; the controls read the view, so a
// change that fails goes back by itself and its message shows under the strip.
export function QaSettingsStrip({ deckId }: Props) {
  const { control, setSettings, setSlide } = useQaControl(deckId);
  const { message: changeMessage, run } = useChangeError();
  if (control === null) return null;
  const { settings, here } = control;
  const questionsLabel =
    settings.questions === 'open'
      ? 'Questions open'
      : settings.questions === 'closed'
        ? 'Questions closed'
        : 'Questions off';
  return (
    <div
      data-testid={IDS.strip}
      role="group"
      aria-label="Question settings"
      className="flex flex-wrap items-center gap-x-3 gap-y-1.5"
    >
      <button
        type="button"
        data-testid={IDS.questions}
        className={TOGGLE}
        aria-pressed={settings.questions === 'open'}
        onClick={() =>
          void run(() =>
            setSettings({ questions: settings.questions === 'open' ? 'closed' : 'open' }),
          )
        }
      >
        {questionsLabel}
      </button>
      <button
        type="button"
        data-testid={IDS.review}
        className={TOGGLE}
        aria-pressed={settings.review}
        onClick={() => void run(() => setSettings({ review: !settings.review }))}
      >
        {settings.review ? 'Review first' : 'Review off'}
      </button>
      <button
        type="button"
        data-testid={IDS.names}
        className={TOGGLE}
        aria-pressed={settings.names}
        onClick={() => void run(() => setSettings({ names: !settings.names }))}
      >
        {settings.names ? 'Names allowed' : 'Names off'}
      </button>
      <button
        type="button"
        data-testid={IDS.reactions}
        className={TOGGLE}
        aria-pressed={settings.reactions}
        onClick={() => void run(() => setSettings({ reactions: !settings.reactions }))}
      >
        {settings.reactions ? 'Reactions on' : 'Reactions off'}
      </button>
      {here !== null && (
        <>
          <QaSlideSwitch
            label="This slide: questions"
            on={here.questions}
            reason={settings.questions === 'off' ? 'Questions are off for the deck' : null}
            onChange={(on) => void run(() => setSlide(here.slide, { questions: on }))}
            testId={IDS.slideQuestions}
            reasonTestId={IDS.slideQuestionsReason}
            labelClass="text-[11.5px] text-[#e2e8f0]"
            reasonClass="text-[11px] text-[#94a3b8]"
          />
          <QaSlideSwitch
            label="This slide: reactions"
            on={here.reactions}
            reason={settings.reactions ? null : 'Reactions are off for the deck'}
            onChange={(on) => void run(() => setSlide(here.slide, { reactions: on }))}
            testId={IDS.slideReactions}
            reasonTestId={IDS.slideReactionsReason}
            labelClass="text-[11.5px] text-[#e2e8f0]"
            reasonClass="text-[11px] text-[#94a3b8]"
          />
        </>
      )}
      {changeMessage !== null && (
        <p role="alert" data-testid={IDS.error} className="basis-full text-[11.5px] text-[#fca5a5]">
          {changeMessage}
        </p>
      )}
    </div>
  );
}

import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
  type RefObject,
} from 'react';
import { useCalmAnnouncement } from '../../polls/guest/useCalmAnnouncement';
import {
  QA_ASK_SHEET_TEST_IDS,
  QA_LIST_TEST_IDS,
  QA_TABS_TEST_IDS,
  questionRowTestId,
} from '../testIds';
import { useAskSheet } from './AskSheetContext';
import { POLL_OPENED_TEXT, PollOpenedBanner } from './PollOpenedBanner';
import { questionErrorText } from './questionErrorText';
import { useQuestions } from './useQuestions';

export type AskSheetProps = {
  presenter: string;
};

const MAX_TEXT = 200;
const MAX_NAME = 30;
// How close to the limit the count is said aloud.
const NEAR_LIMIT = 20;

// The length the server counts: trimmed, with each run of whitespace as one space (QuestionText.Collapse).
function countOf(text: string): number {
  return text.trim().split(/\s+/).filter(Boolean).join(' ').length;
}

const FIELD =
  'box-border min-h-[46px] w-full rounded-xl border-[1.5px] border-(--sw-accent-glow) bg-white/[0.08] px-3.5 text-[15px] text-white placeholder:text-(--sw-night-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)';
const FOCUS_RING =
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)';

// The sheet over the list while useAskSheet().open: the text, the name row (absent when room.names is false), Send, and
// the sending, queued, limited and failed states from useQuestions. It shows PollOpenedBanner while pollJustOpened.
// The draft and the typed name live here, which GuestTabs keeps mounted, so closing the sheet keeps them and the name
// lasts the session; a question that was sent clears the draft.
export function AskSheet({ presenter }: AskSheetProps) {
  const { open, close } = useAskSheet();
  const { asking, ask } = useQuestions();
  const [text, setText] = useState('');
  const [showName, setShowName] = useState(false);
  const [name, setName] = useState('');
  // The trimmed text of the question handed to ask, until it is known to have gone.
  const submitted = useRef<string | null>(null);
  const was = useRef(asking);
  // True once a question has gone, so the sheet's closing sends focus to its row.
  const sent = useRef(false);

  // A question has gone when asking ends in idle from sending or queued. Limited then idle is the countdown running
  // out: nothing was sent, the draft stays.
  useEffect(() => {
    const before = was.current;
    was.current = asking;
    if (asking !== 'idle' || (before !== 'sending' && before !== 'queued')) return;
    if (submitted.current !== null)
      setText((held) => (held.trim() === submitted.current ? '' : held));
    submitted.current = null;
    sent.current = true;
    close();
  }, [asking, close]);

  if (!open) return null;

  const send = (named: string | null) => {
    submitted.current = text.trim();
    void ask(text.trim(), named);
  };

  return (
    <AskSheetDialog
      presenter={presenter}
      text={text}
      onText={setText}
      showName={showName}
      onShowName={setShowName}
      name={name}
      onName={setName}
      onSend={send}
      sentRef={sent}
    />
  );
}

type DialogProps = {
  presenter: string;
  text: string;
  onText(text: string): void;
  showName: boolean;
  onShowName(show: boolean): void;
  name: string;
  onName(name: string): void;
  onSend(name: string | null): void;
  sentRef: RefObject<boolean>;
};

// Where focus goes when the sheet closes: the guest's new question when one was just sent, else where it was when the sheet opened, or the Ask button when the browser did not
// focus it on the tap (Safari does not focus a button when it is tapped), or the Questions tab when the room closed or
// turned questions off here while the sheet was open and the Ask button is gone.
function focusTarget(previous: HTMLElement | null, newQuestion: number | null): HTMLElement | null {
  const row =
    newQuestion === null
      ? null
      : document.querySelector<HTMLElement>(`[data-testid="${questionRowTestId(newQuestion)}"]`);
  if (row) return row;
  if (previous?.isConnected && previous !== document.body) return previous;
  return (
    document.querySelector<HTMLElement>(`[data-testid="${QA_LIST_TEST_IDS.ask}"]`) ??
    document.querySelector<HTMLElement>(`[data-testid="${QA_TABS_TEST_IDS.questionsTab}"]`)
  );
}

function AskSheetDialog({
  presenter,
  text,
  onText,
  showName,
  onShowName,
  name,
  onName,
  onSend,
  sentRef,
}: DialogProps) {
  const { close, pollJustOpened } = useAskSheet();
  const { room, questions, asking, error, errorFrom, retryIn } = useQuestions();
  const dialog = useRef<HTMLDialogElement>(null);
  const field = useRef<HTMLInputElement>(null);

  // The number of the guest's newest question, for the cleanup below, which outlives the render it was made in.
  const newest = useRef<number | null>(null);
  useEffect(() => {
    newest.current = questions.reduce<number | null>(
      (best, q) => (q.yours && (best === null || q.number > best) ? q.number : best),
      null,
    );
  });

  // The keyboard shrinks only the visual viewport on iOS and on Chrome for Android, so the sheet follows it: its bottom
  // edge sits on the visual viewport's and it is no taller than that, which keeps Send above the keyboard.
  useEffect(() => {
    const el = dialog.current;
    const viewport = window.visualViewport;
    if (!el || !viewport) return;
    const follow = () => {
      el.style.bottom = `${Math.max(0, window.innerHeight - (viewport.height + viewport.offsetTop))}px`;
      el.style.maxHeight = `${viewport.height}px`;
    };
    follow();
    viewport.addEventListener('resize', follow);
    viewport.addEventListener('scroll', follow);
    return () => {
      viewport.removeEventListener('resize', follow);
      viewport.removeEventListener('scroll', follow);
    };
  }, []);

  // A modal <dialog> makes the page behind it inert. Focus goes to the text box on opening and back to Ask on closing.
  useLayoutEffect(() => {
    const el = dialog.current;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    sentRef.current = false;
    el?.showModal();
    field.current?.focus({ preventScroll: true });
    return () => {
      if (el?.open) el.close();
      focusTarget(previous, sentRef.current ? newest.current : null)?.focus();
    };
  }, [sentRef]);

  const names = room?.names !== false;
  const trimmedName = name.trim();
  const named = names && showName && trimmedName !== '' ? trimmedName : null;
  const count = countOf(text);
  const busy = asking === 'sending' || asking === 'limited';
  const canSend = count > 0 && count <= MAX_TEXT && !busy;

  const limitedText =
    asking === 'limited' ? questionErrorText('too_many_questions', { presenter, retryIn }) : '';
  const queuedText =
    asking === 'queued'
      ? 'Not sent yet. Your question is kept on this phone and sends by itself when the connection is back.'
      : '';
  const nearLimit =
    count > MAX_TEXT
      ? `${count - MAX_TEXT} characters over ${MAX_TEXT}`
      : count >= MAX_TEXT - NEAR_LIMIT
        ? `${count} of ${MAX_TEXT} characters`
        : '';
  // The countdown and the count change often; a screen reader hears the region at most once every 5 seconds. The poll
  // banner's message goes through it too, because a live region that arrives holding its text is often not read.
  const said = useCalmAnnouncement(
    limitedText || queuedText || (pollJustOpened ? POLL_OPENED_TEXT : '') || nearLimit,
  );

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (canSend) onSend(named);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDialogElement>) => {
    if (event.key !== 'Tab' || !dialog.current) return;
    const stops = [
      ...dialog.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled])',
      ),
    ];
    if (stops.length === 0) return;
    const first = stops[0];
    const last = stops[stops.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return (
    <dialog
      ref={dialog}
      data-testid={QA_ASK_SHEET_TEST_IDS.sheet}
      aria-labelledby="qa-ask-sheet-title"
      onKeyDown={onKeyDown}
      // Esc fires cancel; a tap on the dimmed page behind lands on the dialog itself.
      onCancel={(event) => {
        event.preventDefault();
        close();
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) close();
      }}
      className="fixed inset-x-0 top-auto bottom-0 mx-auto mt-0 mb-0 max-h-[calc(100dvh-0.5rem)] w-full max-w-[480px] overflow-y-auto rounded-t-[20px] border-0 bg-[#161c2c] p-0 text-white backdrop:bg-black/55"
    >
      <form onSubmit={submit} className="relative grid gap-2.5 px-[18px] pt-3.5 pb-[22px]">
        {pollJustOpened && <PollOpenedBanner />}
        <h2
          id="qa-ask-sheet-title"
          data-testid={QA_ASK_SHEET_TEST_IDS.title}
          className="font-head text-base leading-tight font-bold"
        >
          Ask a question
        </h2>
        <button
          type="button"
          data-testid={QA_ASK_SHEET_TEST_IDS.close}
          aria-label="Close"
          onClick={close}
          className={`absolute top-1.5 right-2 grid size-11 cursor-pointer place-items-center rounded-full text-sm text-white ${FOCUS_RING}`}
        >
          ✕
        </button>
        <input
          ref={field}
          type="text"
          enterKeyHint="send"
          autoComplete="off"
          data-testid={QA_ASK_SHEET_TEST_IDS.text}
          aria-label="Your question"
          aria-describedby="qa-ask-sheet-hint qa-ask-sheet-counter"
          aria-invalid={count > MAX_TEXT}
          readOnly={asking === 'sending'}
          placeholder={`What would you like ${presenter} to answer?`}
          value={text}
          onChange={(event) => onText(event.target.value)}
          className={FIELD}
        />
        <div className="-mt-1 flex justify-between gap-3 text-[11.5px] text-(--sw-night-muted)">
          <span id="qa-ask-sheet-hint" data-testid={QA_ASK_SHEET_TEST_IDS.hint}>
            One question at a time works best
          </span>
          <span
            id="qa-ask-sheet-counter"
            data-testid={QA_ASK_SHEET_TEST_IDS.counter}
            className={count > MAX_TEXT ? 'font-bold text-[#fca5a5]' : undefined}
          >
            {count} / {MAX_TEXT}
          </span>
        </div>
        {names && (
          <div data-testid={QA_ASK_SHEET_TEST_IDS.nameRow} className="grid gap-2.5">
            <button
              type="button"
              role="switch"
              aria-checked={showName}
              aria-describedby="qa-ask-sheet-name-hint"
              data-testid={QA_ASK_SHEET_TEST_IDS.nameSwitch}
              onClick={() => onShowName(!showName)}
              className={`flex min-h-11 w-full cursor-pointer items-center gap-2.5 rounded-xl bg-white/[0.06] px-3 py-2.5 text-left text-[13px] text-white ${FOCUS_RING}`}
            >
              <span className="min-w-0 flex-1">Show my name</span>
              <span
                aria-hidden
                className={`relative h-[22px] w-[38px] flex-none rounded-full transition-colors motion-reduce:transition-none ${showName ? 'bg-(--sw-accent)' : 'bg-white/20'}`}
              >
                <span
                  className={`absolute top-0.5 left-0.5 size-[18px] rounded-full bg-white transition-transform motion-reduce:transition-none ${showName ? 'translate-x-4' : ''}`}
                />
              </span>
            </button>
            <p
              id="qa-ask-sheet-name-hint"
              data-testid={QA_ASK_SHEET_TEST_IDS.nameHint}
              className="-mt-1 px-1 text-[11.5px] leading-[1.4] text-(--sw-night-muted)"
            >
              {showName
                ? `The room and ${presenter} see this name with your question.`
                : `Off: nobody sees who asked, not even ${presenter}.`}
            </p>
            {showName && (
              <input
                type="text"
                autoComplete="off"
                maxLength={MAX_NAME}
                data-testid={QA_ASK_SHEET_TEST_IDS.name}
                aria-label="Your name"
                placeholder="Your name"
                readOnly={asking === 'sending'}
                value={name}
                onChange={(event) => onName(event.target.value)}
                className={FIELD}
              />
            )}
          </div>
        )}
        <button
          type="submit"
          data-testid={QA_ASK_SHEET_TEST_IDS.send}
          disabled={!canSend}
          className={`min-h-[46px] cursor-pointer rounded-xl bg-(--sw-accent) px-3 text-[15px] font-bold text-(--sw-on-accent) disabled:cursor-default disabled:opacity-45 ${FOCUS_RING}`}
        >
          {named === null ? 'Send anonymously' : `Send as ${named}`}
        </button>
        {asking === 'sending' && (
          <p
            data-testid={QA_ASK_SHEET_TEST_IDS.sending}
            className="text-center text-[12px] text-(--sw-night-muted)"
          >
            Sending…
          </p>
        )}
        {asking === 'queued' && (
          <p
            data-testid={QA_ASK_SHEET_TEST_IDS.queued}
            className="rounded-[10px] bg-[rgb(165_184_255/0.12)] px-3 py-2.5 text-[12px] leading-[1.4] text-[#dbe3ff]"
          >
            {queuedText}
          </p>
        )}
        {asking === 'limited' && (
          <p
            data-testid={QA_ASK_SHEET_TEST_IDS.limited}
            className="rounded-[10px] bg-[rgb(165_184_255/0.12)] px-3 py-2.5 text-[12px] leading-[1.4] text-[#dbe3ff]"
          >
            {limitedText}
          </p>
        )}
        {asking === 'failed' && errorFrom === 'ask' && (
          <p
            data-testid={QA_ASK_SHEET_TEST_IDS.failed}
            role="alert"
            className="rounded-[10px] bg-[rgb(252_165_165/0.14)] px-3 py-2.5 text-[12.5px] leading-[1.4] text-[#fecaca]"
          >
            {questionErrorText(error ?? '', { presenter })}
          </p>
        )}
        <p aria-live="polite" role="status" className="sr-only">
          {said}
        </p>
        <p
          data-testid={QA_ASK_SHEET_TEST_IDS.finePrint}
          className="text-center text-[11.5px] leading-[1.4] text-(--sw-night-muted)"
        >
          You can't edit a question after sending it.
        </p>
      </form>
    </dialog>
  );
}

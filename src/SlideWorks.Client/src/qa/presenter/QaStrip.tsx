import { useEffect, useRef, useState } from 'react';
import { useCalmAnnouncement } from '../../polls/guest/useCalmAnnouncement';
import { QA_STRIP_TEST_IDS as IDS } from '../testIds';
import type { QaQuestion } from '../types';
import { QaStripSheet, type StripAction } from './QaStripSheet';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
};

const BUTTON =
  'grid min-h-11 min-w-11 place-items-center rounded-[9px] border px-1 text-center text-xs leading-tight font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-300 aria-busy:opacity-60 forced-colors:border-2';
const PLAIN = `${BUTTON} border-white/10 bg-white/10 text-slate-100`;
const PRIMARY = `${BUTTON} border-transparent bg-[#3b5bdb] text-white`;

// The question the strip is about: the one on screen, else the top open one (most votes, ties to the older).
function featured(questions: QaQuestion[], onScreen: number | null): QaQuestion | null {
  const shown = questions.find((q) => q.number === onScreen && q.state === 'open' && !q.hidden);
  if (shown !== undefined) return shown;
  const open = questions.filter((q) => q.state === 'open' && !q.hidden);
  return open.reduce<QaQuestion | null>(
    (top, q) => (top === null || q.votes > top.votes ? q : top),
    null,
  );
}

// The sheet's order: what waits for review first, then the open ones, each by votes and then by age.
function sheetOrder(questions: QaQuestion[]): QaQuestion[] {
  return questions
    .filter((q) => !q.hidden && (q.state === 'open' || q.state === 'waiting'))
    .sort(
      (a, b) =>
        Number(b.state === 'waiting') - Number(a.state === 'waiting') ||
        b.votes - a.votes ||
        a.number - b.number,
    );
}

// The questions strip on the presenter's phone remote (SW-9 Q9), under SW-6's poll strip: the question on screen, or
// else the top open one, with its votes and counts; Show or Take off and Answered; and "All 6", which opens a sheet with
// the open and to-review questions and their buttons. Nothing, and no space, when the deck is not live or has questions
// off.
export function QaStrip({ deckId }: Props) {
  const { control, error, show, unshow, answer, approve, hide } = useQaControl(deckId);
  const root = useRef<HTMLElement>(null);
  const [sheetOpen, setSheetOpen] = useState(false);
  // A tap is ignored while its call is in flight: a second tap would only come back refused.
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const refocus = useRef(false);
  const [settled, setSettled] = useState(0);
  const announced = useCalmAnnouncement(
    control === null ? '' : `${control.counts.open} open, ${control.counts.waiting} to review`,
  );

  // Once the answer is drawn: the button pressed may be gone (the last open question was answered): focus goes to the
  // strip, not to the page.
  useEffect(() => {
    if (!refocus.current) return;
    refocus.current = false;
    if (!sheetOpen && document.activeElement === document.body) root.current?.focus();
  }, [settled, control, sheetOpen]);

  if (
    control === null ||
    !control.live ||
    control.round === null ||
    control.settings.questions === 'off'
  )
    return null;

  const actions: Record<StripAction, (number: number) => Promise<unknown>> = {
    show,
    unshow,
    answer,
    approve,
    hide,
  };
  const run = async (action: StripAction, number: number) => {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    try {
      await actions[action](number);
    } finally {
      sending.current = false;
      setBusy(false);
    }
    refocus.current = true;
    setSettled((n) => n + 1);
  };

  const { counts } = control;
  const question = featured(control.questions, control.onScreen);
  const total = counts.open + counts.waiting;
  const up = question !== null && question.onScreen;
  const button = (testId: string, label: string, action: StripAction, look: string) => (
    <button
      type="button"
      data-testid={testId}
      aria-label={`${label}: ${question!.text}`}
      aria-busy={busy || undefined}
      className={look}
      onClick={() => void run(action, question!.number)}
    >
      {label}
    </button>
  );

  return (
    <section
      ref={root}
      tabIndex={-1}
      data-testid={IDS.strip}
      aria-label="Questions"
      className="mx-4 mt-3 grid gap-2 rounded-xl border border-gray-800 bg-[#111827] p-2.5 outline-none"
    >
      {question === null ? (
        <p data-testid={IDS.empty} className="py-1 text-sm text-slate-300">
          No open questions
          {counts.waiting > 0 && (
            <span data-testid={IDS.counts} className="text-slate-400">
              {' · '}
              {counts.waiting} to review
            </span>
          )}
        </p>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            {up && (
              <span
                data-testid={IDS.onScreen}
                className="rounded-full border border-[#4ade80] px-2 py-0.5 text-xs font-semibold text-[#86efac] forced-colors:border-2"
              >
                On screen
              </span>
            )}
            <b data-testid={IDS.votes} className="font-mono text-lg leading-none">
              {question.votes}
            </b>
            <span data-testid={IDS.counts} className="text-xs text-slate-400">
              votes · {counts.open} open
              {counts.waiting > 0 && ` · ${counts.waiting} to review`}
            </span>
          </div>
          <p
            data-testid={IDS.text}
            className="line-clamp-2 text-sm leading-snug [overflow-wrap:anywhere] text-slate-100"
          >
            {question.text}
          </p>
        </>
      )}
      <div className="grid grid-cols-3 gap-1.5">
        {question !== null &&
          (up ? (
            <>
              {button(IDS.answer, 'Answered', 'answer', PRIMARY)}
              {button(IDS.takeOff, 'Take off', 'unshow', PLAIN)}
            </>
          ) : (
            <>
              {button(IDS.show, 'Show', 'show', PRIMARY)}
              {button(IDS.answer, 'Answered', 'answer', PLAIN)}
            </>
          ))}
        {total > 0 && (
          <button
            type="button"
            data-testid={IDS.all}
            aria-haspopup="dialog"
            onClick={() => setSheetOpen(true)}
            className={`${PLAIN} col-start-3`}
          >
            All {total}
          </button>
        )}
      </div>
      <span role="status" aria-live="polite" className="sr-only">
        {announced}
      </span>
      {error !== null && !sheetOpen && (
        <p data-testid={IDS.error} role="alert" className="text-sm text-red-300">
          {error}
        </p>
      )}
      {sheetOpen && (
        <QaStripSheet
          questions={sheetOrder(control.questions)}
          error={error}
          onAction={run}
          onClose={() => setSheetOpen(false)}
        />
      )}
    </section>
  );
}

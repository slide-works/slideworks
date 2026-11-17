import { useEffect, useRef, useState, type ReactNode } from 'react';
import type { PollPhase } from '../types';
import { POLL_STRIP_TEST_IDS } from '../testIds';
import { usePollControl } from './usePollControl';

const PHASE_LABEL: Record<PollPhase, string> = { draft: 'Draft', open: 'Open', closed: 'Closed' };

const BUTTON =
  'grid min-h-12 min-w-11 place-items-center rounded-[9px] border px-1 text-center text-xs leading-tight font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-300 aria-busy:opacity-60 forced-colors:border-2';
const PLAIN = `${BUTTON} border-white/10 bg-white/10 text-slate-100`;
const PRIMARY = `${BUTTON} border-transparent bg-[#3b5bdb] text-white`;
const STOP = `${BUTTON} border-red-300/40 bg-red-600/25 text-red-200`;

// The poll strip on the presenter's phone remote (SW-6 V8), between the slide and the notes: the state, the answer count
// and the rate, and the controls the phase allows. Nothing at all, and no space, on a slide with no poll.
//
// The three control slots keep their columns so a tap never lands on a different control than the one aimed at when
// the phase changes under the thumb: Open or Close in the first, Reveal or Show answer in the second, New round in the
// third. Close and New round never share a column.
export function PollStrip({ deckId }: { deckId: string }) {
  const { control, open, close, reveal, showAnswer, newRound, error } = usePollControl(deckId);
  const stripRef = useRef<HTMLElement>(null);
  // Whether focus was last inside the strip, to tell a button that went away under the presenter's thumb (the phase
  // moved on) from focus the presenter put elsewhere.
  const focusInside = useRef(false);
  // A tap is ignored while its call is in flight: a second tap would only come back refused.
  const [busy, setBusy] = useState(false);
  const phase = control?.phase;

  useEffect(() => {
    if (focusInside.current && document.activeElement === document.body) stripRef.current?.focus();
  }, [phase]);

  if (control === null) return null;
  const { answered, joined, question, settings } = control;
  const run = (action: () => Promise<void>) => async () => {
    if (busy) return;
    setBusy(true);
    try {
      await action();
    } finally {
      setBusy(false);
    }
  };
  const name = (verb: string) => `${verb}: ${question}`;
  const button = (
    testId: string,
    label: string,
    verb: string,
    action: () => Promise<void>,
    placement: string,
    look: string,
  ): ReactNode => (
    <button
      type="button"
      data-testid={testId}
      aria-label={name(verb)}
      aria-busy={busy || undefined}
      className={`${look} ${placement}`}
      onClick={run(action)}
    >
      {label}
    </button>
  );

  return (
    <section
      ref={stripRef}
      tabIndex={-1}
      data-testid={POLL_STRIP_TEST_IDS.strip}
      aria-label="Poll"
      onFocus={() => {
        focusInside.current = true;
      }}
      onBlur={(event) => {
        if (event.relatedTarget !== null && !event.currentTarget.contains(event.relatedTarget))
          focusInside.current = false;
      }}
      className="mx-4 mt-3 grid gap-2.5 rounded-xl border border-gray-800 bg-[#111827] p-2.5 outline-none"
    >
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span
          data-testid={POLL_STRIP_TEST_IDS.state}
          data-phase={control.phase}
          className="rounded-full border border-white/25 px-2 py-0.5 text-xs font-semibold text-slate-100 forced-colors:border-2"
        >
          {PHASE_LABEL[control.phase]}
        </span>
        <b data-testid={POLL_STRIP_TEST_IDS.answered} className="font-mono text-lg leading-none">
          {answered}
        </b>{' '}
        <span className="text-xs text-slate-400">{answered === 1 ? 'answer' : 'answers'}</span>
        {joined > 0 && (
          <>
            <span aria-hidden className="text-xs text-slate-400">
              ·
            </span>
            <span data-testid={POLL_STRIP_TEST_IDS.rate} className="text-xs text-slate-400">
              {Math.round((answered / joined) * 100)}% of {joined}
            </span>
          </>
        )}
      </div>
      <div className="grid grid-cols-3 gap-1.5">
        {control.phase === 'draft' &&
          button(POLL_STRIP_TEST_IDS.open, 'Open', 'Open poll', open, 'col-span-3', PRIMARY)}
        {control.phase === 'open' && (
          <>
            {button(POLL_STRIP_TEST_IDS.close, 'Close', 'Close poll', close, 'col-start-1', STOP)}
            {!control.roomSeesCounts &&
              button(
                POLL_STRIP_TEST_IDS.reveal,
                'Reveal',
                'Reveal results',
                reveal,
                'col-start-2',
                PLAIN,
              )}
          </>
        )}
        {control.phase === 'closed' && (
          <>
            {settings.correct !== null &&
              !control.answerShown &&
              button(
                POLL_STRIP_TEST_IDS.showAnswer,
                'Show answer',
                'Show answer',
                showAnswer,
                'col-start-2',
                PLAIN,
              )}
            {button(
              POLL_STRIP_TEST_IDS.newRound,
              'New round',
              'New round',
              newRound,
              'col-start-3',
              PLAIN,
            )}
          </>
        )}
      </div>
      {error !== null && (
        <p data-testid={POLL_STRIP_TEST_IDS.error} role="alert" className="text-sm text-red-300">
          {error}
        </p>
      )}
    </section>
  );
}

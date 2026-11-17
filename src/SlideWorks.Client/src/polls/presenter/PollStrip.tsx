import { useEffect, useRef, useState, type ReactNode } from 'react';
import type { PollPhase } from '../types';
import { useCalmAnnouncement } from '../guest/useCalmAnnouncement';
import { POLL_STRIP_TEST_IDS } from '../testIds';
import { usePollControl } from './usePollControl';

const PHASE_LABEL: Record<PollPhase, string> = { draft: 'Draft', open: 'Open', closed: 'Closed' };

const BUTTON =
  'grid min-h-12 min-w-11 place-items-center [@media(max-height:600px)]:min-h-11 rounded-[9px] border px-1 text-center text-xs leading-tight font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-300 aria-busy:opacity-60 forced-colors:border-2';
const PLAIN = `${BUTTON} border-white/10 bg-white/10 text-slate-100`;
const PRIMARY = `${BUTTON} border-transparent bg-[#3b5bdb] text-white`;
const STOP = `${BUTTON} border-red-300/40 bg-red-600/25 text-red-200`;

// How long taps are ignored after a tap or after the phase changes under the thumb. A control that appears where the
// last tap landed (Close where Open was, on a double tap or a fast response) must not take that tap. The buttons are
// not drawn disabled for this time, so the strip does not flicker.
const TAP_GUARD_MS = 500;

// The poll strip on the presenter's phone remote (SW-6 V8), between the slide and the notes: the state, the answer count
// and the rate, and the controls the phase allows. Nothing at all, and no space, on a slide with no poll.
//
// On a short screen (height up to 600 px) the strip is compact. An error is a message laid over the slide preview above
// the strip, at every height, instead of a row in it: a row would make the page taller than a short screen and push
// Slides, Skip and Blackout off. The preview is not a control; the message's dismiss button, or the next tap on a
// control, clears it.
//
// Columns: Close in the first, Reveal and Show answer in the second, Open and New round in the third, so Close never
// shares a column with Open or New round. The tap guard covers the rest.
export function PollStrip({ deckId }: { deckId: string }) {
  const { control, open, close, reveal, showAnswer, newRound, error } = usePollControl(deckId);
  const stripRef = useRef<HTMLElement>(null);
  // Whether focus was last inside the strip, to tell a button that went away under the presenter's thumb (the phase
  // moved on) from focus the presenter put elsewhere.
  const focusInside = useRef(false);
  // A tap is ignored while its call is in flight: a second tap would only come back refused.
  const [busy, setBusy] = useState(false);
  // The error the presenter dismissed. The provider keeps an error until the next call, so a dismissed one stays hidden
  // until then; the next tap on a control clears this and shows whatever that call answers.
  const [dismissed, setDismissed] = useState<string | null>(null);
  const phase = control?.phase;
  const announced = useCalmAnnouncement(
    control === null ? '' : `${control.answered} ${control.answered === 1 ? 'answer' : 'answers'}`,
  );
  // Taps are ignored while this is true; a timer ends it, so no clock is read.
  const guarded = useRef(false);
  const guardTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const guard = () => {
    guarded.current = true;
    if (guardTimer.current !== null) clearTimeout(guardTimer.current);
    guardTimer.current = setTimeout(() => {
      guarded.current = false;
      guardTimer.current = null;
    }, TAP_GUARD_MS);
  };
  // The poll the thumb is looking at: a new slide, round or phase starts the guard (not the first one on the page).
  const poll = control === null ? 'none' : `${control.slide}:${control.number}:${control.phase}`;
  const lastPoll = useRef(poll);
  useEffect(() => {
    if (lastPoll.current !== poll) {
      lastPoll.current = poll;
      guard();
    }
    // guard only touches refs
  }, [poll]);
  useEffect(
    () => () => {
      if (guardTimer.current !== null) clearTimeout(guardTimer.current);
    },
    [],
  );

  useEffect(() => {
    if (focusInside.current && document.activeElement === document.body) stripRef.current?.focus();
  }, [phase]);

  if (control === null) return null;
  const { answered, joined, question, settings } = control;
  const run = (action: () => Promise<void>) => async () => {
    if (busy || guarded.current) return;
    guard();
    setDismissed(null);
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
      aria-label={`Poll, ${control.phase}: ${question}`}
      onFocus={() => {
        focusInside.current = true;
      }}
      onBlur={(event) => {
        // Focus left the strip for another element, or for the page (a tap on the notes): a later phase change must
        // not pull it back. A button removed under the presenter keeps the flag, as it is no longer connected.
        if (
          (event.relatedTarget === null ? event.target.isConnected : true) &&
          !(
            event.relatedTarget instanceof Node && event.currentTarget.contains(event.relatedTarget)
          )
        )
          focusInside.current = false;
      }}
      className="relative mx-4 mt-3 grid gap-2.5 rounded-xl border border-gray-800 bg-[#111827] p-2.5 outline-none [@media(max-height:600px)]:mt-1 [@media(max-height:600px)]:gap-1 [@media(max-height:600px)]:p-1.5"
    >
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span
          data-testid={POLL_STRIP_TEST_IDS.state}
          data-phase={control.phase}
          className="rounded-full border border-white/25 px-2 py-0.5 [@media(max-height:600px)]:py-0 text-xs font-semibold text-slate-100 forced-colors:border-2"
        >
          {PHASE_LABEL[control.phase]}
        </span>
        <b
          aria-hidden
          data-testid={POLL_STRIP_TEST_IDS.answered}
          className="font-mono text-lg leading-none"
        >
          {answered}
        </b>{' '}
        <span aria-hidden className="text-xs text-slate-400">
          {answered === 1 ? 'answer' : 'answers'}
        </span>
        {joined > 0 && (
          <>
            <span aria-hidden className="text-xs text-slate-400">
              ·
            </span>
            <span data-testid={POLL_STRIP_TEST_IDS.rate} className="text-xs text-slate-400">
              {Math.min(100, Math.round((answered / joined) * 100))}% of {joined}
            </span>
          </>
        )}
      </div>
      <div className="grid grid-cols-3 gap-1.5">
        {control.phase === 'draft' &&
          button(POLL_STRIP_TEST_IDS.open, 'Open', 'Open poll', open, 'col-start-3', PRIMARY)}
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
      <span role="status" aria-live="polite" className="sr-only">
        {announced}
      </span>
      {error !== null && error !== dismissed && (
        <div className="flex items-start gap-1 rounded-xl border border-red-300/40 bg-red-950 p-2 absolute inset-x-0 bottom-full z-10 mb-1">
          <p
            data-testid={POLL_STRIP_TEST_IDS.error}
            role="alert"
            className="min-w-0 flex-1 py-2.5 text-sm text-red-200"
          >
            {error}
          </p>
          <button
            type="button"
            aria-label="Dismiss this message"
            className="grid size-11 shrink-0 place-items-center rounded-lg border border-white/25 text-lg leading-none text-red-100 focus-visible:outline-2 focus-visible:outline-indigo-300 forced-colors:border-2"
            onClick={() => setDismissed(error)}
          >
            <span aria-hidden>×</span>
          </button>
        </div>
      )}
    </section>
  );
}

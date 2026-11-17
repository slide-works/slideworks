import { useRef, useState } from 'react';
import {
  usePresenterPollKeys,
  type PresenterPollKeyActions,
} from '../../presentation/presenter/usePresenterKeys';
import type { PollControlView, PollPhase, PollResults } from '../types';
import { POLL_PANEL_TEST_IDS, pollPanelHistoryRoundTestId } from '../testIds';
import { PollPanelBars } from './PollPanelBars';
import { availablePollActions, PollPanelButtons } from './PollPanelButtons';
import { usePollControl, type PollAction } from './usePollControl';

type Props = {
  deckId: string;
  // Something is open on top of the presenter window (the overview, the phone dialog): the poll's keys do nothing.
  overlayOpen?: boolean;
};

const PHASE_LABELS: Readonly<Record<PollPhase, string>> = {
  draft: 'Draft',
  open: 'Open',
  closed: 'Closed',
};

const PHASE_DOTS: Readonly<Record<PollPhase, string>> = {
  draft: 'bg-[#94a3b8]',
  open: 'bg-[#4ade80]',
  closed: 'bg-[#8ea5ff]',
};

const RESULTS_LABELS: Readonly<Record<PollResults, string>> = {
  live: 'Live',
  afterAnswer: 'After answering',
  afterClose: 'After close',
};

// "Pick one · Live", "Pick up to 2 · After close"
function settingsOf({ settings }: PollControlView): string {
  const pick =
    settings.pick === 'one'
      ? 'Pick one'
      : settings.max === null
        ? 'Pick several'
        : `Pick up to ${settings.max}`;
  return `${pick} · ${RESULTS_LABELS[settings.results]}`;
}

// "93% of 41 joined". joined is the guests connected now, which drops as phones sleep or people leave after answering,
// so only the percentage is capped at 100; the numbers are shown as they are. No percentage when nobody is joined.
function rateOf({ answered, joined }: { answered: number; joined: number }): string {
  if (joined === 0) return '0 joined';
  return `${Math.min(100, Math.round((answered / joined) * 100))}% of ${joined} joined`;
}

// What the room sees. The presenter always sees the counts; this says whether the room does too (roomSeesCounts).
function roomOf(control: PollControlView): string {
  if (control.phase === 'draft')
    return `Round ${control.number} is not open yet. The room sees it once you open it.`;
  if (control.phase === 'closed') return 'The room sees the final result.';
  if (control.revealed) return 'Revealed: the room sees these results.';
  if (control.roomSeesCounts) return 'The room sees these results.';
  return control.settings.results === 'afterAnswer'
    ? 'Hidden from the room. Each guest sees them after answering.'
    : 'Hidden from the room until the poll closes.';
}

const answersOf = (count: number) => (count === 1 ? '1 answer' : `${count} answers`);

// The poll panel in the presenter window (SW-6 V8): the poll's state and round, its settings, the answer count and the
// rate, a bar per option with its count, what the room sees, the controls the phase allows, earlier rounds of this
// session and the keys (P open or close, R reveal, A show answer). On a slide with no poll it shows nothing on screen:
// only its "No poll on this slide." for assistive technology, so SW-4's window looks as it did. A failed call's message
// shows in the panel, also on a slide whose poll couldn't be read.
export function PollPanel({ deckId, overlayOpen = false }: Props) {
  const poll = usePollControl(deckId);
  const { control, error } = poll;
  const [busy, setBusy] = useState(false);
  // The same, read synchronously, so two presses before a render still send one call.
  const sending = useRef(false);
  const root = useRef<HTMLElement>(null);

  const run = async (action: PollAction) => {
    if (sending.current) return;
    sending.current = true;
    const hadFocus = root.current?.contains(document.activeElement) ?? false;
    setBusy(true);
    const send: Record<PollAction, () => Promise<void>> = {
      open: poll.open,
      close: poll.close,
      reveal: poll.reveal,
      showAnswer: poll.showAnswer,
      newRound: poll.newRound,
    };
    await send[action]();
    sending.current = false;
    setBusy(false);
    // The button pressed may be gone now (Close gives way to New round): keep focus in the panel, not on the page.
    if (hadFocus && !root.current?.contains(document.activeElement)) root.current?.focus();
  };

  const available = control === null ? null : availablePollActions(control);
  const keys: PresenterPollKeyActions | null =
    available === null || busy
      ? null
      : {
          togglePoll: available.has('open')
            ? () => void run('open')
            : available.has('close')
              ? () => void run('close')
              : undefined,
          reveal: available.has('reveal') ? () => void run('reveal') : undefined,
          showAnswer: available.has('showAnswer') ? () => void run('showAnswer') : undefined,
        };
  usePresenterPollKeys(keys, overlayOpen);

  if (control === null && error === null) {
    return (
      <section data-testid={POLL_PANEL_TEST_IDS.panel} aria-label="Poll" className="sr-only">
        <p data-testid={POLL_PANEL_TEST_IDS.empty}>No poll on this slide.</p>
      </section>
    );
  }

  const earlier = control?.rounds.filter((round) => round.number < control.number) ?? [];

  return (
    <section
      ref={root}
      data-testid={POLL_PANEL_TEST_IDS.panel}
      aria-label={control ? `Poll: ${control.question}` : 'Poll'}
      tabIndex={-1}
      className="grid min-w-0 content-start gap-2 outline-none"
    >
      <h2 className="text-[11px] font-semibold tracking-[0.12em] text-[#94a3b8] uppercase">Poll</h2>
      <div className="grid gap-3 rounded-lg border border-[#1f2937] bg-[#111827] px-4 py-3">
        {control === null ? (
          <p data-testid={POLL_PANEL_TEST_IDS.empty} className="text-[#94a3b8] italic">
            No poll on this slide.
          </p>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <span
                data-testid={POLL_PANEL_TEST_IDS.state}
                className="inline-flex items-center gap-1.5 rounded-full bg-white/5 px-2 py-0.5 text-[11px] font-semibold tracking-[0.08em] uppercase forced-colors:border forced-colors:border-[CanvasText]"
              >
                <span
                  className={`size-1.5 rounded-full ${PHASE_DOTS[control.phase]}`}
                  aria-hidden="true"
                />
                {PHASE_LABELS[control.phase]}
              </span>
              <span
                data-testid={POLL_PANEL_TEST_IDS.round}
                className="font-mono text-xs text-[#94a3b8]"
              >
                round {control.number}
              </span>
              <span
                data-testid={POLL_PANEL_TEST_IDS.settings}
                className="ml-auto text-xs text-[#94a3b8]"
              >
                {settingsOf(control)}
              </span>
            </div>
            <p className="truncate font-semibold" title={control.question}>
              {control.question}
            </p>
            <div className="flex flex-wrap items-baseline gap-x-2">
              <span data-testid={POLL_PANEL_TEST_IDS.answered}>
                <span className="text-2xl font-bold tabular-nums">{control.answered}</span>{' '}
                {control.answered === 1 ? 'answer' : 'answers'}
              </span>
              <span
                data-testid={POLL_PANEL_TEST_IDS.rate}
                className="ml-auto rounded-md bg-[#1e2a4a] px-2 py-0.5 text-xs text-[#a5b8ff]"
              >
                {rateOf(control)}
              </span>
            </div>
            <PollPanelBars control={control} />
            <p data-testid={POLL_PANEL_TEST_IDS.room} className="text-xs text-[#94a3b8]">
              {roomOf(control)}
            </p>
            <PollPanelButtons control={control} busy={busy} onAction={(a) => void run(a)} />
          </>
        )}
        {error !== null && (
          <p
            data-testid={POLL_PANEL_TEST_IDS.error}
            role="alert"
            className="rounded-md bg-[#c0362c]/25 px-3 py-2 text-sm text-[#fca5a5]"
          >
            {error}
          </p>
        )}
        {earlier.length > 0 && (
          <ul
            data-testid={POLL_PANEL_TEST_IDS.history}
            aria-label="Earlier rounds"
            className="grid gap-0.5 border-t border-[#1f2937] pt-2 text-xs text-[#94a3b8]"
          >
            {earlier.map((round) => (
              <li key={round.number} data-testid={pollPanelHistoryRoundTestId(round.number)}>
                Round {round.number}: {answersOf(round.answered)}, {rateOf(round)}, kept
              </li>
            ))}
          </ul>
        )}
        {control !== null && (
          <p data-testid={POLL_PANEL_TEST_IDS.keys} className="text-[11px] text-[#94a3b8]">
            <kbd className="rounded border border-[#334155] px-1 font-mono">P</kbd> open or close
            the poll · <kbd className="rounded border border-[#334155] px-1 font-mono">R</kbd>{' '}
            reveal · <kbd className="rounded border border-[#334155] px-1 font-mono">A</kbd> show
            answer
          </p>
        )}
      </div>
    </section>
  );
}

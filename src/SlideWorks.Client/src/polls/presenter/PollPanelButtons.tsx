import type { PollControlView } from '../types';
import { POLL_PANEL_TEST_IDS } from '../testIds';
import type { PollAction } from './usePollControl';

// Which of the five actions the poll's phase allows (SW-6 V4), the one rule the buttons and the keys share: Open from
// draft; Close from open; Reveal while open and hidden from the room; Show answer once closed, when the poll has a
// correct answer not yet shown; New round once closed.
export function availablePollActions(control: PollControlView): ReadonlySet<PollAction> {
  const available = new Set<PollAction>();
  switch (control.phase) {
    case 'draft':
      available.add('open');
      break;
    case 'open':
      available.add('close');
      if (!control.roomSeesCounts) available.add('reveal');
      break;
    case 'closed':
      if (control.settings.correct !== null && !control.answerShown) available.add('showAnswer');
      available.add('newRound');
      break;
  }
  return available;
}

// Close stops answers, so it reads as a stop; Open and Show answer move the poll on; the rest are plain.
const TONES = {
  go: 'bg-[#2f5bea] text-white hover:bg-[#3d68f0]',
  stop: 'bg-[#4a1d24] text-[#fecaca] hover:bg-[#5c232c]',
  plain: 'bg-white/10 text-[#e2e8f0] hover:bg-white/15',
} as const;

// Each button's text, accessible name (its subject, the question, included) and test id, in the order they show.
const BUTTONS: readonly {
  action: PollAction;
  label: string;
  name: (question: string) => string;
  testId: string;
  tone: keyof typeof TONES;
}[] = [
  {
    action: 'open',
    label: 'Open poll',
    name: (q) => `Open poll: ${q}`,
    testId: POLL_PANEL_TEST_IDS.open,
    tone: 'go',
  },
  {
    action: 'close',
    label: 'Close poll',
    name: (q) => `Close poll: ${q}`,
    testId: POLL_PANEL_TEST_IDS.close,
    tone: 'stop',
  },
  {
    action: 'reveal',
    label: 'Reveal results',
    name: (q) => `Reveal results: ${q}`,
    testId: POLL_PANEL_TEST_IDS.reveal,
    tone: 'plain',
  },
  {
    action: 'showAnswer',
    label: 'Show answer',
    name: (q) => `Show answer: ${q}`,
    testId: POLL_PANEL_TEST_IDS.showAnswer,
    tone: 'go',
  },
  {
    action: 'newRound',
    label: 'New round',
    name: (q) => `New round: ${q}`,
    testId: POLL_PANEL_TEST_IDS.newRound,
    tone: 'plain',
  },
];

// The poll panel's controls: only those the phase allows. While a call is on its way they are disabled (aria-disabled,
// so focus stays on the button pressed), and the second click of a double click does nothing, so one press sends one
// call.
export function PollPanelButtons({
  control,
  busy,
  onAction,
}: {
  control: PollControlView;
  busy: boolean;
  onAction(action: PollAction): void;
}) {
  const available = availablePollActions(control);
  return (
    <div className="flex flex-wrap gap-2">
      {BUTTONS.filter((button) => available.has(button.action)).map((button) => (
        <button
          key={button.action}
          type="button"
          data-testid={button.testId}
          aria-label={button.name(control.question)}
          aria-disabled={busy || undefined}
          className={`min-h-9 rounded-md px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-[#8ea5ff] aria-disabled:opacity-60 ${TONES[button.tone]}`}
          onClick={(event) => {
            // The second click of a double click is ignored: the phase may have moved on under the pointer, and the
            // button now there (Close where Open was) is not the one aimed at.
            if (!busy && event.detail <= 1) onAction(button.action);
          }}
        >
          {button.label}
        </button>
      ))}
    </div>
  );
}

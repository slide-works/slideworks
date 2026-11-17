import type { PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';
import { PollRadio } from './PollRadio';

// What the options may draw: one count per option and how many have answered, so a bar is count ÷ answered. Null while
// the phone may not show counts.
export type PollBars = { counts: number[]; answered: number };

export type PollOptionsProps = {
  // The id of the card's question: the group's label.
  questionId: string;
  // The options and the correct option once it is shown.
  activity: PollActivity;
  // Nothing can be tapped: the card is loading, or the poll isn't open (draft or closed). The options still show.
  disabled: boolean;
  // What is selected on the phone, 0-based.
  picks: number[];
  // The picks the server holds for this guest, for the "You" mark, or null before the first send.
  mine: number[] | null;
  bars: PollBars | null;
  onToggle: (position: number) => void;
};

const shareOf = (bars: PollBars, position: number) =>
  bars.answered > 0 ? Math.round(((bars.counts[position] ?? 0) / bars.answered) * 100) : 0;

// Pick one: a radio per option, at least 44 px tall. A tap sends the answer (D3), and a tap on another option changes
// it; focus stays on the option tapped. Bars show only when the card passes them.
export function PollOptions({
  questionId,
  activity,
  disabled,
  picks,
  mine,
  bars,
  onToggle,
}: PollOptionsProps) {
  return (
    <div
      data-testid={POLL_CARD_TEST_IDS.options}
      role="radiogroup"
      aria-labelledby={questionId}
      className="grid gap-1.5"
    >
      {activity.options.map((text, position) => (
        <PollRadio
          // Options are known by their position (D13): the key keeps the tapped radio, and its focus, across renders.
          key={position}
          questionId={questionId}
          position={position}
          text={text}
          checked={picks.includes(position)}
          you={mine?.includes(position) ?? false}
          correct={activity.correct === position + 1}
          share={bars === null ? null : shareOf(bars, position)}
          disabled={disabled}
          dimmed={activity.phase === 'draft'}
          onToggle={onToggle}
        />
      ))}
    </div>
  );
}

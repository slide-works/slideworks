import type { PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';

// What the options may draw: one count per option and how many have answered, so a bar is count ÷ answered. Null while
// the phone may not show counts.
export type PollBars = { counts: number[]; answered: number };

export type PollOptionsProps = {
  // The id of the card's question: the group's label.
  questionId: string;
  // The options, the phase (they can be picked only while open) and the correct option once it is shown.
  activity: PollActivity;
  // What is selected on the phone, 0-based.
  picks: number[];
  // The picks the server holds for this guest, for the "You" mark, or null before the first send.
  mine: number[] | null;
  bars: PollBars | null;
  onToggle: (position: number) => void;
};

// Pick one: a radio per option, at least 44 px tall. A tap sends the answer (D3).
export function PollOptions({ questionId }: PollOptionsProps) {
  // TODO: draw the radios, the "You" and correct marks and the bars; until then the group is empty.
  return (
    <div data-testid={POLL_CARD_TEST_IDS.options} role="radiogroup" aria-labelledby={questionId} />
  );
}

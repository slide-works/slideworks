import { POLL_CARD_TEST_IDS } from '../testIds';

export type PollVerdictProps = {
  // The 1-based correct option, once the presenter has shown it.
  correct: number;
  options: string[];
  // The picks the server holds for this guest, or null when it sent none.
  mine: number[] | null;
};

// After the presenter shows the answer: "You got it." or, in amber because it isn't an error, "Not this time. The
// answer was …". No scores (D6).
export function PollVerdict({ correct, options }: PollVerdictProps) {
  // TODO: say whether the guest got it; until then it names the answer.
  return (
    <p data-testid={POLL_CARD_TEST_IDS.verdict}>{`The answer was “${options[correct - 1]}”.`}</p>
  );
}

import { POLL_CARD_TEST_IDS } from '../testIds';

export type PollVerdictProps = {
  // The 1-based correct option, once the presenter has shown it.
  correct: number;
  options: string[];
  // The picks the server holds for this guest, or null when it sent none.
  mine: number[] | null;
};

const LOOK = {
  told: 'bg-(--sw-paper) text-(--sw-ink)',
  right: 'bg-(--sw-good-soft) text-(--sw-good-ink)',
  miss: 'bg-(--sw-warn-soft) text-(--sw-warn-ink)',
} as const;

// After the presenter shows the answer: "You got it." or, in amber because it isn't an error, "Not this time. The
// answer was …". No scores (D6). A guest who sent nothing is only told the answer.
export function PollVerdict({ correct, options, mine }: PollVerdictProps) {
  const answer = `The answer was “${options[correct - 1]}”.`;
  const outcome = mine === null ? 'told' : mine.includes(correct - 1) ? 'right' : 'miss';
  return (
    <div
      data-testid={POLL_CARD_TEST_IDS.verdict}
      role="status"
      className={`rounded-[10px] px-[11px] py-[9px] text-[13px] leading-snug font-semibold ${LOOK[outcome]}`}
    >
      {outcome === 'told' && <p>{answer}</p>}
      {outcome === 'right' && <p data-testid={POLL_CARD_TEST_IDS.verdictRight}>You got it.</p>}
      {outcome === 'miss' && (
        <p data-testid={POLL_CARD_TEST_IDS.verdictMiss}>{`Not this time. ${answer}`}</p>
      )}
    </div>
  );
}

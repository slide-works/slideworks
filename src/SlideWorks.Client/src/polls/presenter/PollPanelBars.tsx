import type { PollControlView } from '../types';
import {
  POLL_PANEL_TEST_IDS,
  pollPanelBarTestId,
  pollPanelCorrectTestId,
  pollPanelCountTestId,
  pollPanelRowTestId,
} from '../testIds';

// One row per option in the poll panel: its text, a bar and its count. The presenter always sees the counts, whatever
// the room sees (SW-6 V4). A bar is the share of people who answered that picked the option, so with pick several they
// can add up to more than the whole. Once the answer is shown, the correct row is marked in words, not colour alone.
// Counts change in place: nothing here takes or moves focus.
export function PollPanelBars({ control }: { control: PollControlView }) {
  const { options, counts, answered, settings, answerShown } = control;
  const correct = answerShown ? settings.correct : null;
  return (
    <ul
      data-testid={POLL_PANEL_TEST_IDS.bars}
      aria-label="Answers per option"
      className="grid gap-1.5"
    >
      {options.map((option, position) => {
        const count = counts[position] ?? 0;
        const share = answered > 0 ? Math.min(100, Math.round((count / answered) * 100)) : 0;
        const isCorrect = correct === position + 1;
        return (
          <li
            key={position}
            data-testid={pollPanelRowTestId(position)}
            className="grid grid-cols-[minmax(0,1fr)_minmax(4rem,8rem)_2.5rem] items-center gap-3"
          >
            <span className="min-w-0 truncate" title={option}>
              {option}
              {isCorrect && (
                <span
                  data-testid={pollPanelCorrectTestId(position)}
                  className="ml-2 rounded bg-[#14532d] px-1.5 py-0.5 text-[11px] font-semibold text-[#86efac]"
                >
                  ✓ Correct
                </span>
              )}
            </span>
            <span className="h-1.5 overflow-hidden rounded-full bg-white/10" aria-hidden="true">
              <span
                data-testid={pollPanelBarTestId(position)}
                className={`block h-full rounded-full ${isCorrect ? 'bg-[#4ade80]' : 'bg-[#6d8cff]'}`}
                style={{ width: `${share}%` }}
              />
            </span>
            <span
              data-testid={pollPanelCountTestId(position)}
              className="text-right font-mono tabular-nums"
            >
              {count}
            </span>
          </li>
        );
      })}
    </ul>
  );
}

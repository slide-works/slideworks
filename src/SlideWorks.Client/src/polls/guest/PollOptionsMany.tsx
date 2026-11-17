import { useId } from 'react';
import { POLL_CARD_TEST_IDS } from '../testIds';
import { PollCheckbox } from './PollCheckbox';
import type { PollBars, PollOptionsProps } from './PollOptions';

// Pick several: the same props as PollOptions. activity.max caps the picks.
export type PollOptionsManyProps = PollOptionsProps;

// A share of the answers, a whole percentage. With pick several the shares add up to more than 100%.
const shareOf = (bars: PollBars, position: number) =>
  bars.answered > 0 ? Math.round(((bars.counts[position] ?? 0) / bars.answered) * 100) : 0;

// Pick several: a checkbox per option, at least 44 px tall. A tap only selects, up to max; at the limit the unpicked
// options are disabled, and a note a screen reader reads with each says why. Nothing is sent here: PollFoot has Send
// (D3).
export function PollOptionsMany({
  questionId,
  activity,
  disabled,
  picks,
  mine,
  bars,
  onToggle,
}: PollOptionsManyProps) {
  const limitNoteId = useId();
  const max = activity.max ?? activity.options.length;
  const atLimit = !disabled && picks.length >= max;

  return (
    <div
      data-testid={POLL_CARD_TEST_IDS.optionsMany}
      role="group"
      aria-labelledby={questionId}
      className="grid gap-1.5"
    >
      {atLimit && (
        <p id={limitNoteId} className="sr-only">
          {`You have picked ${max}, the most you can. Untick one to pick another.`}
        </p>
      )}
      {activity.options.map((text, position) => {
        const checked = picks.includes(position);
        const blocked = atLimit && !checked;
        return (
          <PollCheckbox
            // Options are known by their position (D13): the key keeps the tapped box, and its focus, across renders.
            key={position}
            questionId={questionId}
            position={position}
            text={text}
            checked={checked}
            you={mine?.includes(position) ?? false}
            correct={activity.correct === position + 1}
            share={bars === null ? null : shareOf(bars, position)}
            disabled={disabled || blocked}
            dimmed={activity.phase === 'draft' || blocked}
            limitNoteId={blocked ? limitNoteId : null}
            onToggle={onToggle}
          />
        );
      })}
    </div>
  );
}

import { POLL_CARD_TEST_IDS } from '../testIds';
import type { PollOptionsProps } from './PollOptions';

// Pick several: the same props as PollOptions. activity.max caps the picks.
export type PollOptionsManyProps = PollOptionsProps;

// Pick several: a checkbox per option, at least 44 px tall. A tap only selects, up to max; at the limit the other
// options grey out. Nothing is sent until Send (D3).
export function PollOptionsMany({ questionId }: PollOptionsManyProps) {
  // TODO: draw the checkboxes, the limit, the "You" and correct marks and the bars; until then the group is empty.
  return (
    <div data-testid={POLL_CARD_TEST_IDS.optionsMany} role="group" aria-labelledby={questionId} />
  );
}

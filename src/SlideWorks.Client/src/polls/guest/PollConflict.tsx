import { useEffect, useRef } from 'react';
import type { PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';

export type PollConflictProps = {
  // The poll as it is now: its question names the buttons.
  activity: PollActivity;
  presenter: string;
  // Send it: sends the kept pick again.
  onSend: () => void;
  // Clear: drops the kept pick.
  onClear: () => void;
};

const BUTTON =
  'flex min-h-11 flex-1 items-center justify-center rounded-xl px-3 text-[14px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-dark)';

// A 409 came back: the answer went against an old round or revision. The pick is kept and the card asks again. Focus
// moves to the message when the card appears, so a screen reader reads it once, on focus (no alert role on top). The
// props don't say which round the pick was sent against, so it says only that the poll changed.
export function PollConflict({ activity, presenter, onSend, onClear }: PollConflictProps) {
  const message = useRef<HTMLParagraphElement>(null);
  useEffect(() => {
    message.current?.focus();
  }, []);

  return (
    <div
      data-testid={POLL_CARD_TEST_IDS.conflict}
      className="grid gap-2 rounded-xl bg-(--sw-warn-soft) px-3 py-2.5 text-(--sw-warn-ink)"
    >
      <p
        ref={message}
        tabIndex={-1}
        data-testid={POLL_CARD_TEST_IDS.conflictMessage}
        className="text-[13px] leading-snug font-semibold outline-none"
      >
        {`${presenter} changed the poll. Send this pick again?`}
      </p>
      <div className="flex gap-2">
        <button
          type="button"
          data-testid={POLL_CARD_TEST_IDS.conflictSend}
          aria-label={`Send it: ${activity.question}`}
          onClick={onSend}
          className={`${BUTTON} bg-(--sw-accent) text-(--sw-on-accent)`}
        >
          Send it
        </button>
        <button
          type="button"
          data-testid={POLL_CARD_TEST_IDS.conflictClear}
          aria-label={`Clear: ${activity.question}`}
          onClick={onClear}
          className={`${BUTTON} border-[1.5px] border-(--sw-grid) bg-(--sw-paper) text-(--sw-ink)`}
        >
          Clear
        </button>
      </div>
    </div>
  );
}

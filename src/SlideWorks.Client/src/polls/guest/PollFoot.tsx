import type { ReactNode } from 'react';
import type { MyAnswer, PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';
import type { PollAnswerState } from './usePollAnswer';
import { useCalmAnnouncement } from './useCalmAnnouncement';

export type PollFootProps = {
  // The phase, pick, max and results setting decide what the foot says.
  activity: PollActivity;
  // The presenter's name, for "Results show … when <presenter> closes the poll" and "<presenter> hasn't opened this
  // poll yet".
  presenter: string;
  // What is selected on the phone, for "1 of 2 picked" and Send.
  picks: number[];
  // The answer the server holds, or null: "Answer sent", "You didn't send an answer for this one".
  mine: MyAnswer | null;
  // How many have answered so far, from the latest tally, or null before one arrives. Always shown (D21).
  answered: number | null;
  // Closed with no answer: true for "You joined after this poll closed", false for "You didn't send an answer".
  joinedLate: boolean;
  // Never 'conflict' or 'queued': PollConflict and PollQueued take the foot's place then. 'loading' says nothing yet.
  state: PollAnswerState;
  // A failed call's message, when state is 'failed'.
  error: string | null;
  // Pick several's Send, and sending again after a failure.
  onSend: () => void;
  // Pick several's "Change my answer": clears the picks on the phone to choose again.
  onClear: () => void;
};

const answersOf = (answered: number) => `${answered} ${answered === 1 ? 'answer' : 'answers'}`;

const samePicks = (a: number[], b: number[]) =>
  a.length === b.length && a.every((pick, i) => pick === b[i]);

function Note({ testId, children }: { testId: string; children: ReactNode }) {
  return (
    <p
      data-testid={testId}
      className="rounded-lg bg-(--sw-paper) px-2.5 py-2 text-[12px] leading-snug text-(--sw-muted)"
    >
      {children}
    </p>
  );
}

function Hint({ children, centred = false }: { children: ReactNode; centred?: boolean }) {
  return (
    <div
      className={`flex flex-wrap gap-x-2 text-[12px] text-(--sw-muted) ${centred ? 'justify-center' : 'justify-between'}`}
    >
      {children}
    </div>
  );
}

function FootButton({
  testId,
  label,
  ghost = false,
  disabled = false,
  onClick,
  children,
}: {
  testId: string;
  label: string;
  ghost?: boolean;
  disabled?: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  const look = ghost
    ? 'border-[1.5px] border-(--sw-grid) bg-(--sw-paper) text-(--sw-ink) text-[13.5px]'
    : 'bg-(--sw-accent) text-(--sw-on-accent) text-[15px] disabled:bg-(--sw-grid) disabled:text-(--sw-muted)';
  return (
    <button
      type="button"
      data-testid={testId}
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
      className={`flex min-h-11 items-center justify-center rounded-xl px-3 font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-dark) disabled:cursor-default ${look}`}
    >
      {children}
    </button>
  );
}

function Sent() {
  return (
    <p
      data-testid={POLL_CARD_TEST_IDS.sent}
      className="flex items-center gap-1.5 text-[13px] font-semibold text-(--sw-good-ink)"
    >
      <svg
        aria-hidden
        width="15"
        height="15"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="3"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M5 12.5l4.5 4.5L19 7.5" />
      </svg>
      Answer sent
    </p>
  );
}

// What the card says under the options: the hint, Send, sent, the answer count, closed, not open yet, a failure. The
// count while open changes as answers arrive; a polite live region announces it at most once every 5 seconds.
export function PollFoot({
  activity,
  presenter,
  picks,
  mine,
  answered,
  joinedLate,
  state,
  error,
  onSend,
  onClear,
}: PollFootProps) {
  const question = activity.question;
  const many = activity.pick === 'upTo';
  const open = activity.phase === 'open';
  // Pick several after "Change my answer": the phone's picks differ from the answer sent, so Send is back.
  const choosing = mine === null || (many && !samePicks(picks, mine.picks));
  const counting = open && !choosing && answered !== null;
  const announced = useCalmAnnouncement(counting ? `${answersOf(answered)} so far` : '');

  const failed = state === 'failed' && (
    <div
      role="alert"
      className="grid gap-2 rounded-lg bg-(--sw-bad-soft) px-2.5 py-2 text-[12px] leading-snug text-(--sw-bad-ink)"
    >
      <p className="font-semibold">Your answer wasn’t sent.</p>
      <p data-testid={POLL_CARD_TEST_IDS.failed}>{error ?? 'Something went wrong.'}</p>
      {open && picks.length > 0 && (
        <FootButton
          testId={POLL_CARD_TEST_IDS.send}
          label={`Send again: ${question}`}
          onClick={onSend}
        >
          Send again
        </FootButton>
      )}
    </div>
  );

  let lines: ReactNode;
  if (state === 'loading') lines = null;
  else if (activity.phase === 'draft')
    lines = (
      <Note testId={POLL_CARD_TEST_IDS.notOpen}>
        {`${presenter} hasn’t opened this poll yet. It opens by itself, so keep this page open.`}
      </Note>
    );
  else if (activity.phase === 'closed')
    lines = (
      <>
        <div data-testid={POLL_CARD_TEST_IDS.closed}>
          <Hint>
            <span>Poll closed</span>
            {answered !== null && <span>{answersOf(answered)}</span>}
          </Hint>
        </div>
        {mine === null &&
          (joinedLate ? (
            <Note testId={POLL_CARD_TEST_IDS.joinedLate}>You joined after this poll closed.</Note>
          ) : (
            <Note testId={POLL_CARD_TEST_IDS.noAnswer}>
              You didn’t send an answer for this one.
            </Note>
          ))}
        {failed}
      </>
    );
  else if (state === 'failed') lines = failed;
  else if (state === 'sending')
    lines = (
      <p
        data-testid={POLL_CARD_TEST_IDS.sending}
        role="status"
        className="text-[13px] font-semibold text-(--sw-muted)"
      >
        Sending your answer…
      </p>
    );
  else if (choosing && many)
    lines = (
      <>
        <div data-testid={POLL_CARD_TEST_IDS.pickCount}>
          <Hint>
            <span>{`${picks.length} of ${activity.max} picked`}</span>
            <span>You can change it later</span>
          </Hint>
        </div>
        <FootButton
          testId={POLL_CARD_TEST_IDS.send}
          label={`Send answer: ${question}`}
          disabled={picks.length === 0}
          onClick={onSend}
        >
          Send answer
        </FootButton>
      </>
    );
  else if (choosing)
    lines = (
      <p
        data-testid={POLL_CARD_TEST_IDS.tapHint}
        className="rounded-lg bg-(--sw-paper) px-2.5 py-2 text-center text-[12px] leading-snug text-(--sw-muted)"
      >
        <b className="text-[13px] font-semibold text-(--sw-ink)">Tap an answer to send it.</b>
        <br />
        You can change it while the poll is open.
      </p>
    );
  else
    lines = (
      <>
        <Sent />
        {counting && (
          <Hint>
            <span data-testid={POLL_CARD_TEST_IDS.answered}>{`${answersOf(answered)} so far`}</span>
            <span>Still open</span>
          </Hint>
        )}
        {activity.results === 'afterClose' && !activity.roomSeesCounts && (
          <Note testId={POLL_CARD_TEST_IDS.resultsLater}>
            {`Results show here and on the big screen when ${presenter} closes the poll.`}
          </Note>
        )}
        {many ? (
          <FootButton
            testId={POLL_CARD_TEST_IDS.change}
            label={`Change my answer: ${question}`}
            ghost
            onClick={onClear}
          >
            Change my answer
          </FootButton>
        ) : (
          <div data-testid={POLL_CARD_TEST_IDS.changeHint}>
            <Hint centred>
              <span>Tap another option to change it</span>
            </Hint>
          </div>
        )}
      </>
    );

  return (
    <div data-testid={POLL_CARD_TEST_IDS.foot} className="grid gap-2">
      {lines}
      <p aria-live="polite" className="sr-only">
        {announced}
      </p>
    </div>
  );
}

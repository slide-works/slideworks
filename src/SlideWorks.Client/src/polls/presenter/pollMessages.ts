import { ApiError } from '../../api/client';
import type { PollRefusalCode } from '../types';
import type { PollAction } from './usePollControl';

// What the presenter is told when a poll call fails (SW-6 V8). A refusal's code is never shown as it is: each one says
// what happened and what to do next. Shared by the poll panel and the phone remote's strip through usePollControl.

export const POLL_READ_FAILED =
  "Couldn't load this slide's poll. Check your connection; it loads again when you move or reconnect.";

// Each refusal code (V4) as a sentence. The provider reads the poll again after one, so the buttons already show what
// can be done now.
export const POLL_REFUSAL_MESSAGES: Readonly<Record<PollRefusalCode, string>> = {
  not_live: "The deck isn't live, so the poll can't open. Go live first, then open it.",
  wrong_phase: 'The poll had already moved on. The buttons now show what you can do.',
  another_poll_open: 'A poll on another slide is still open. Close it first, then open this one.',
  already_visible: 'The room already sees these results.',
  no_correct: 'This poll has no correct answer to show.',
};

const ACTION_NAMES: Readonly<Record<PollAction, string>> = {
  open: 'open the poll',
  close: 'close the poll',
  reveal: 'reveal the results',
  showAnswer: 'show the answer',
  newRound: 'start a new round',
};

const isRefusal = (code: string): code is PollRefusalCode =>
  Object.hasOwn(POLL_REFUSAL_MESSAGES, code);

// The message for a failed action: the refusal's sentence for a 409, else what went wrong and what to do.
export function pollActionFailed(action: PollAction, failure: unknown): string {
  const what = `Couldn't ${ACTION_NAMES[action]}.`;
  if (!(failure instanceof ApiError)) return `${what} Check your connection and try again.`;
  if (failure.status === 409 && isRefusal(failure.message))
    return POLL_REFUSAL_MESSAGES[failure.message];
  if (failure.status === 409)
    return `${what} The poll had changed. The buttons now show what you can do.`;
  if (failure.status === 404) return `${what} This slide has no poll any more.`;
  if (failure.status === 403)
    return `${what} Only people who can present this deck can run its polls.`;
  return `${what} Try again in a moment.`;
}

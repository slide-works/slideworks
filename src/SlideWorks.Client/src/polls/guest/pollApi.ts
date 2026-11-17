import { ApiError, api } from '../../api/client';
import type { AnswerRequest, GuestPollView, MyAnswer, StaleAnswer } from '../types';

const pollPath = (code: string) => `/api/join/${encodeURIComponent(code)}/poll`;

// GET /api/join/{code}/poll (V5): the activity, this guest's answer and the tally it may see. Throws ApiError: 400
// no_guest, 404 no_session.
export function getGuestPoll(code: string): Promise<GuestPollView> {
  return api.get<GuestPollView>(pollPath(code));
}

// What a PUT came back with when the server answered with an answer: 200 accepted it, 409 refused it as stale and says
// what the phone should show now. Nothing was written on a 409.
export type AnswerOutcome =
  { type: 'accepted'; answer: MyAnswer } | { type: 'stale'; current: GuestPollView };

// PUT /api/join/{code}/poll/answer (V5). A 409 is an outcome, not an error, because its body carries the current view,
// which api/client.ts would drop. Any other refusal throws ApiError with the body's error: a 400 names the rule
// ("picks: at most 2") or no_guest, 404 is no_session, 429 is too_many_answers. A call that never reached the server
// rejects with fetch's own TypeError.
export async function putPollAnswer(code: string, body: AnswerRequest): Promise<AnswerOutcome> {
  const response = await fetch(`${pollPath(code)}/answer`, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (response.ok) return { type: 'accepted', answer: (await response.json()) as MyAnswer };
  const refusal = await refusalOf(response);
  if (response.status === 409 && refusal?.error === 'stale')
    return { type: 'stale', current: (refusal as StaleAnswer).current };
  throw new ApiError(refusal?.error ?? `Request failed (${response.status})`, response.status);
}

async function refusalOf(response: Response): Promise<{ error?: string } | null> {
  try {
    return (await response.json()) as { error?: string };
  } catch {
    return null;
  }
}

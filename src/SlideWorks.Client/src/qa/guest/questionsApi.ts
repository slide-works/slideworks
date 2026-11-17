import { ApiError, api } from '../../api/client';
import type {
  AskRefusalCode,
  AskRequest,
  AskedQuestion,
  GuestQuestionsView,
  MyVotes,
  ReactionEmoji,
  StaleVotes,
  TooManyQuestions,
  VoteRequest,
} from '../types';

const questionsPath = (code: string) => `/api/join/${encodeURIComponent(code)}/questions`;

// GET /api/join/{code}/questions (Q4): the room, the questions this guest may see and its upvotes. Throws ApiError: 400
// no_guest, 404 no_session, 429 too_many_attempts.
export function getQuestions(code: string): Promise<GuestQuestionsView> {
  return api.get<GuestQuestionsView>(questionsPath(code));
}

// What a POST came back with when the server answered: 201 the question, 409 one of ASK_REFUSALS (nothing written),
// 429 past 3 questions a minute with whole seconds to wait.
export type AskOutcome =
  | { type: 'asked'; asked: AskedQuestion }
  | { type: 'refused'; error: AskRefusalCode }
  | { type: 'limited'; retryAfter: number };

// POST /api/join/{code}/questions (Q4). Any other refusal throws ApiError with the body's error: a 400 names the rule
// ("text: at most 200") or no_guest, 404 is no_session, 413 a body over its limit, 429 too_many_attempts. A call that
// never reached the server rejects with fetch's own TypeError.
export async function postQuestion(code: string, body: AskRequest): Promise<AskOutcome> {
  const response = await send('POST', questionsPath(code), body);
  if (response.ok) return { type: 'asked', asked: (await response.json()) as AskedQuestion };
  const refusal = await refusalOf(response);
  if (response.status === 409 && refusal?.error)
    return { type: 'refused', error: refusal.error as AskRefusalCode };
  if (response.status === 429 && refusal?.error === 'too_many_questions')
    return { type: 'limited', retryAfter: (refusal as TooManyQuestions).retryAfter };
  throw failure(response, refusal);
}

// What a PUT came back with when the server answered with votes: 200 stored them, 409 stale says what the phone should
// show now. Nothing was written on a 409.
export type VoteOutcome =
  | { type: 'accepted'; mine: MyVotes }
  | { type: 'stale'; current: GuestQuestionsView }
  | { type: 'limited' };

// PUT /api/join/{code}/questions/votes (Q4). 429 too_many_votes is an outcome. Any other refusal throws ApiError: 400
// "picks: unknown question 7", "picks: duplicate" or no_guest, 404 no_session, 409 questions_off, 413, 429
// too_many_attempts. A call that never reached the server rejects with fetch's own TypeError.
export async function putVotes(code: string, body: VoteRequest): Promise<VoteOutcome> {
  const response = await send('PUT', `${questionsPath(code)}/votes`, body);
  if (response.ok) return { type: 'accepted', mine: (await response.json()) as MyVotes };
  const refusal = await refusalOf(response);
  if (response.status === 409 && refusal?.error === 'stale')
    return { type: 'stale', current: (refusal as StaleVotes).current };
  if (response.status === 429 && refusal?.error === 'too_many_votes') return { type: 'limited' };
  throw failure(response, refusal);
}

// What a reaction tap came back with: 204 counted it, 409 reactions_off, 429 past 10 taps in 5 seconds.
export type ReactionOutcome = { type: 'sent' } | { type: 'off' } | { type: 'limited' };

// POST /api/join/{code}/reactions (Q7). Any other refusal throws ApiError: 400 "emoji: unknown" or no_guest, 404
// no_session, 413, 429 too_many_attempts. A call that never reached the server rejects with fetch's own TypeError.
export async function postReaction(code: string, emoji: ReactionEmoji): Promise<ReactionOutcome> {
  const response = await send('POST', `/api/join/${encodeURIComponent(code)}/reactions`, { emoji });
  if (response.ok) return { type: 'sent' };
  const refusal = await refusalOf(response);
  if (response.status === 409 && refusal?.error === 'reactions_off') return { type: 'off' };
  if (response.status === 429 && refusal?.error === 'too_many_reactions')
    return { type: 'limited' };
  throw failure(response, refusal);
}

// Raw fetch, because the 409 and 429 bodies carry more than api/client.ts keeps.
function send(method: 'POST' | 'PUT', path: string, body: unknown): Promise<Response> {
  return fetch(path, {
    method,
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

function failure(response: Response, refusal: { error?: string } | null): ApiError {
  return new ApiError(refusal?.error ?? `Request failed (${response.status})`, response.status);
}

async function refusalOf(response: Response): Promise<{ error?: string } | null> {
  try {
    return (await response.json()) as { error?: string };
  } catch {
    return null;
  }
}

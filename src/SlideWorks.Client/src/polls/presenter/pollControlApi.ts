import { api } from '../../api/client';
import type { PollControlView } from '../types';
import type { PollAction } from './usePollControl';

// The presenter's poll routes (SW-6 V4). `slide` is the 1-based slide number; the server resolves it at call time.
// Every route answers with the poll's PollControlView. A refusal is an ApiError with status 409 and the refusal code
// (POLL_REFUSALS) as its message; 404 when the deck can't be viewed, 403 when the caller can view but not present.

// The path segment of each action's POST.
export const POLL_ACTION_PATHS: Readonly<Record<PollAction, string>> = {
  open: 'open',
  close: 'close',
  reveal: 'reveal',
  showAnswer: 'answer',
  newRound: 'round',
};

export function pollPath(deckId: string, slide: number): string {
  return `/api/decks/${encodeURIComponent(deckId)}/polls/${slide}`;
}

// GET /api/decks/{id}/polls/{slide}
export function readPollControl(deckId: string, slide: number): Promise<PollControlView> {
  return api.get<PollControlView>(pollPath(deckId, slide));
}

// POST /api/decks/{id}/polls/{slide}/open, /close, /reveal, /answer or /round
export function sendPollAction(
  deckId: string,
  slide: number,
  action: PollAction,
): Promise<PollControlView> {
  return api.post<PollControlView>(`${pollPath(deckId, slide)}/${POLL_ACTION_PATHS[action]}`);
}

export const openPoll = (deckId: string, slide: number) => sendPollAction(deckId, slide, 'open');
export const closePoll = (deckId: string, slide: number) => sendPollAction(deckId, slide, 'close');
export const revealPoll = (deckId: string, slide: number) =>
  sendPollAction(deckId, slide, 'reveal');
export const showPollAnswer = (deckId: string, slide: number) =>
  sendPollAction(deckId, slide, 'showAnswer');
export const startPollRound = (deckId: string, slide: number) =>
  sendPollAction(deckId, slide, 'newRound');

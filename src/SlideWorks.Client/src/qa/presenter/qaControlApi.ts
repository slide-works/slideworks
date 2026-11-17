import { api } from '../../api/client';
import type { QaAction, QaControlView, QaSettingsPatch, QaSlidePatch } from '../types';

// The presenter's Q&A routes (SW-9 Q6). Every route answers with the deck's QaControlView. A refusal is an ApiError
// with status 409 and the refusal code (QA_REFUSALS) as its message; an unknown question number is 404; 404 also when
// the deck can't be viewed, 403 when the caller can view but not present. The question routes answer 409 not_live
// while the deck isn't live; the settings routes work live or not.

export function qaPath(deckId: string): string {
  return `/api/decks/${encodeURIComponent(deckId)}/qa`;
}

// GET /api/decks/{id}/qa
export function readQaControl(deckId: string): Promise<QaControlView> {
  return api.get<QaControlView>(qaPath(deckId));
}

// POST /api/decks/{id}/qa/questions/{number}/approve, /hide, /restore, /answer, /reopen, /show or /unshow
export function sendQaAction(
  deckId: string,
  action: QaAction,
  number: number,
): Promise<QaControlView> {
  return api.post<QaControlView>(`${qaPath(deckId)}/questions/${number}/${action}`);
}

// PUT /api/decks/{id}/qa/settings, with any of the five settings.
export function updateQaSettings(deckId: string, patch: QaSettingsPatch): Promise<QaControlView> {
  return api.put<QaControlView>(`${qaPath(deckId)}/settings`, patch);
}

// PUT /api/decks/{id}/qa/slides/{slide}, the 1-based slide's switch; either field may be left out.
export function updateQaSlide(
  deckId: string,
  slide: number,
  patch: QaSlidePatch,
): Promise<QaControlView> {
  return api.put<QaControlView>(`${qaPath(deckId)}/slides/${slide}`, patch);
}

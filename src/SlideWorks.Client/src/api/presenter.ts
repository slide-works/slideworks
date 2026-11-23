import { api } from './client';
import type { LivePresentationsView, PresenterView } from './types';

// The presenter window's and the phone's one request (P3). The owner or a person with write access (C5): anyone else gets a 404, the same as a missing
// deck, so a caller tells "not yours" from other failures by ApiError's status.
export function getPresenterView(deckId: string): Promise<PresenterView> {
  return api.get<PresenterView>(`/api/decks/${encodeURIComponent(deckId)}/presenter`);
}

// The signed-in person's decks that are being presented now, newest first (P4): the phone's pick page.
export function getLivePresentations(): Promise<LivePresentationsView> {
  return api.get<LivePresentationsView>('/api/presentations');
}

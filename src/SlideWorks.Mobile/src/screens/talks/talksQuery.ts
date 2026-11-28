import { api } from '@slideworks/core/client';
import type { DecksLibrarySummary } from '@slideworks/core/decks';
import { signedInKey } from '../../queries/queryClient';

// GET /api/decks under the signed-in keys, so a 401 and sign-out clear it and the persisted cache shows it at once.
export const talksQueryKey = signedInKey('decks');

export const talksQuery = {
  queryKey: talksQueryKey,
  queryFn: () => api.get<DecksLibrarySummary>('/api/decks'),
};

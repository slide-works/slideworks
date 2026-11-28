import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister';
import { QueryClient, type QueryKey } from '@tanstack/react-query';
import { ApiError } from '@slideworks/core/client';
import type { KeyValueStore } from '@slideworks/core/platform';

// The app's one QueryClient (contract K10), persisted to the kv store so the talk list shows at once after a restart.
// Queries that need the device session live under keys starting ['signedIn'], which a 401 and sign-out clear.

export const SIGNED_IN_QUERY_KEY = ['signedIn'] as const;
export const QUERY_CACHE_KEY = 'slideworks.queries';
// The persisted cache is kept a day; a query must stay in memory at least that long to be persisted.
export const PERSIST_MAX_AGE_MS = 24 * 60 * 60 * 1000;

export function signedInKey(...parts: unknown[]): QueryKey {
  return [...SIGNED_IN_QUERY_KEY, ...parts];
}

// A refusal (4xx) won't change on retry; a 5xx or a network failure might.
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status < 500) return false;
  return failureCount < 3;
}

export function createAppQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { gcTime: PERSIST_MAX_AGE_MS, retry: shouldRetry } },
  });
}

export function createQueryPersister(kv: KeyValueStore) {
  return createSyncStoragePersister({ storage: kv, key: QUERY_CACHE_KEY });
}

export function clearSignedInQueries(client: QueryClient): void {
  client.removeQueries({ queryKey: SIGNED_IN_QUERY_KEY });
}

import { vi } from 'vitest';
import { configurePlatform, defaultPlatform, type Platform } from '../platform';

// An app-like platform for the core's tests: a signed-in origin with a bearer, guest headers per origin, and spies on
// the two handlers.
export function configureTestPlatform(overrides: Partial<Platform> = {}) {
  const defaults = defaultPlatform();
  const onUnauthorized = vi.fn();
  const onPendingApproval = vi.fn();
  configurePlatform({
    ...defaults,
    apiOrigin: () => 'https://signed-in.example',
    authHeaders: () => ({ Authorization: 'Bearer swd_secret' }),
    guestHeaders: (origin) => ({
      'X-SlideWorks-Client': 'app',
      'X-SlideWorks-Guest': `id-for-${origin}`,
    }),
    credentials: () => 'omit',
    onUnauthorized,
    onPendingApproval,
    ...overrides,
  });
  return { onUnauthorized, onPendingApproval };
}

// Replaces fetch with one that answers every call with `status`, `body` and `headers`, and records the calls.
export function stubFetch(status: number, body?: unknown, headers: Record<string, string> = {}) {
  const fetchMock = vi.fn<(input: string, init: RequestInit) => Promise<Response>>(
    async () => new Response(body === undefined ? null : JSON.stringify(body), { status, headers }),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

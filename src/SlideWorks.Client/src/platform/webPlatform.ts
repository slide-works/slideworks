import type { Platform } from '@slideworks/core/platform';

// The browser's side of the core's platform seam (contract K1). The session is the browser's cookie, sent on
// same-origin requests only, so there are no headers to add and the origin is the page's own. The two handlers are
// passed in by api/client.ts, which keeps the setters the pages and tests use.
export function webPlatform(handlers: {
  onUnauthorized: () => void;
  onPendingApproval: () => void;
}): Platform {
  return {
    persistentStore: () => localStorage,
    sessionStore: () => sessionStorage,
    isOffline: () => !navigator.onLine,
    onOnline: (listener) => {
      window.addEventListener('online', listener);
      return () => window.removeEventListener('online', listener);
    },
    apiOrigin: () => '',
    authHeaders: () => ({}),
    guestHeaders: () => ({}),
    credentials: () => 'same-origin',
    onUnauthorized: () => handlers.onUnauthorized(),
    onPendingApproval: () => handlers.onPendingApproval(),
  };
}

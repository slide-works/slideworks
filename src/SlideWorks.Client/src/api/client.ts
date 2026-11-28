// The SPA's API client is the core's (@slideworks/core/client, contract K2), running on the web platform. The session is
// the browser's cookie, sent on same-origin requests only, and the browser adds Origin and Sec-Fetch-Site itself, which
// the server's cross-site write guard checks. A 401 from any call mid-session (an expired cookie) goes to the registered
// handler, which by default sends the person to sign in and back. A 403 { error: 'pending_approval' } goes to its own
// handler, which by default sends the person to /pending.
import { configurePlatform } from '@slideworks/core/platform';
import { webPlatform } from '../platform/webPlatform';

export {
  ApiError,
  api,
  type ApiFailureBody,
  type ApiFieldError,
  type RequestOptions,
} from '@slideworks/core/client';

type SignInLocation = Pick<Location, 'pathname' | 'search' | 'hash' | 'assign'>;

const SIGN_IN_PATH = '/signin';

// Sends the person to /signin with where they were (path, query and hash) as returnTo, the same shape App.tsx uses.
// Already on /signin, it stays put: a 401 there would otherwise nest returnTo inside returnTo, or loop.
export function redirectToSignIn(location: SignInLocation): void {
  if (location.pathname === SIGN_IN_PATH) return;
  const returnTo = location.pathname + location.search + location.hash;
  location.assign(`${SIGN_IN_PATH}?returnTo=${encodeURIComponent(returnTo)}`);
}

let onUnauthorized: () => void = () => redirectToSignIn(window.location);

export function setOnUnauthorized(handler: () => void): void {
  onUnauthorized = handler;
}

const PENDING_PATH = '/pending';

// Sends a person whose account isn't approved yet to /pending. Already there, it stays put.
export function redirectToPending(location: Pick<Location, 'pathname' | 'assign'>): void {
  if (location.pathname === PENDING_PATH) return;
  location.assign(PENDING_PATH);
}

let onPendingApproval: () => void = () => redirectToPending(window.location);

export function setOnPendingApproval(handler: () => void): void {
  onPendingApproval = handler;
}

// Configured on first import, not in main.tsx, so Vitest and the component tests, which never run main.tsx, get the
// web behaviour too.
configurePlatform(
  webPlatform({
    onUnauthorized: () => onUnauthorized(),
    onPendingApproval: () => onPendingApproval(),
  }),
);

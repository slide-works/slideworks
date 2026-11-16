// Single fetch wrapper for all API calls. The session is the browser's cookie, sent on same-origin requests only, and
// the browser adds Origin and Sec-Fetch-Site itself, which the server's cross-site write guard checks. It parses the
// API's { error } body on failure, and a 401 from any call mid-session (an expired cookie) goes to the registered
// handler, which by default sends the person to sign in and back. A 403 { error: 'pending_approval' } goes to its own
// handler, which by default sends the person to /pending.

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

// GET /api/me is how the app learns whether anyone is signed in. Its 401 is an answer for the caller (the auth gate in
// App.tsx), not a session that expired, so it never reaches the handler.
const SESSION_CHECK_PATH = '/api/me';

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
const PENDING_APPROVAL_ERROR = 'pending_approval';

// Sends a person whose account isn't approved yet to /pending. Already there, it stays put.
export function redirectToPending(location: Pick<Location, 'pathname' | 'assign'>): void {
  if (location.pathname === PENDING_PATH) return;
  location.assign(PENDING_PATH);
}

let onPendingApproval: () => void = () => redirectToPending(window.location);

export function setOnPendingApproval(handler: () => void): void {
  onPendingApproval = handler;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    credentials: 'same-origin',
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : {},
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (response.status === 401) {
    if (path !== SESSION_CHECK_PATH) onUnauthorized();
    throw new ApiError(await errorMessage(response), 401);
  }
  if (!response.ok) {
    const message = await errorMessage(response);
    if (
      response.status === 403 &&
      message === PENDING_APPROVAL_ERROR &&
      path !== SESSION_CHECK_PATH
    ) {
      onPendingApproval();
    }
    throw new ApiError(message, response.status);
  }
  return response.status === 204 ? (undefined as T) : response.json();
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const parsed = (await response.json()) as { error?: string };
    return parsed.error ?? `Request failed (${response.status})`;
  } catch {
    return `Request failed (${response.status})`;
  }
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body: unknown) => request<T>('PUT', path, body),
  del: <T>(path: string) => request<T>('DELETE', path),
};

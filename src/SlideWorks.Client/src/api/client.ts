// Single fetch wrapper for all API calls. The session is the browser's cookie, sent on same-origin requests only, and
// the browser adds Origin and Sec-Fetch-Site itself, which the server's cross-site write guard checks. It parses the
// API's { error } body on failure, and a 401 from any call mid-session (an expired cookie) goes to the registered
// handler, which by default sends the person to sign in and back.

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

// Sends the person to /signin with where they were (path, query and hash) as returnTo, the same shape App.tsx uses.
export function redirectToSignIn(location: SignInLocation): void {
  const returnTo = location.pathname + location.search + location.hash;
  location.assign(`/signin?returnTo=${encodeURIComponent(returnTo)}`);
}

let onUnauthorized: () => void = () => redirectToSignIn(window.location);

export function setOnUnauthorized(handler: () => void): void {
  onUnauthorized = handler;
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
    throw new ApiError(await errorMessage(response), response.status);
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

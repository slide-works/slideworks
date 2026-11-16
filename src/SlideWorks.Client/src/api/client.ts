// Single fetch wrapper for all API calls. It parses the API's { error } body on
// failure, and (with token auth) attaches the bearer token and funnels every 401
// back to the token screen via the registered handler.
const TOKEN_KEY = 'slide-works-api-token';

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

let onUnauthorized: () => void = () => {};

export function setOnUnauthorized(handler: () => void): void {
  onUnauthorized = handler;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: {
      Authorization: `Bearer ${getToken() ?? ''}`,
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (response.status === 401) {
    clearToken();
    onUnauthorized();
    throw new ApiError('Not authorized', 401);
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

import { platform } from './platform';

// The one fetch wrapper for every API call, on the web and in the app (contract K2). Where a request goes and what proves
// who sent it come from the platform: a 'person' request goes to apiOrigin() with authHeaders() (the web's cookie rides
// along on its own), a 'guest' request goes to the origin it names with guestHeaders(origin). It parses the API's
// { error } body on failure, and keeps a refusal's message, path and errors on the ApiError. A 401 on a person request
// mid-session goes to platform().onUnauthorized; a 403 { error: 'pending_approval' } goes to onPendingApproval.

// One entry of a refusal body's `errors`: the field it is about, its code and its text.
export type ApiFieldError = { path: string; code: string; message: string };

// What a failure body carried beyond the text ApiError.message holds. `code` is the body's `error`
// as sent: a code on the routes that refuse with { error, message, path?, errors? }, the sentence
// itself on the routes that answer { error: "<sentence>" }. `detail` is the body's `message`.
export type ApiFailureBody = {
  code?: string;
  detail?: string;
  path?: string;
  errors?: ApiFieldError[];
};

export class ApiError extends Error {
  public readonly code?: string;
  public readonly detail?: string;
  public readonly path?: string;
  public readonly errors?: ApiFieldError[];

  constructor(
    message: string,
    public readonly status: number,
    body: ApiFailureBody = {},
  ) {
    super(message);
    if (body.code !== undefined) this.code = body.code;
    if (body.detail !== undefined) this.detail = body.detail;
    if (body.path !== undefined) this.path = body.path;
    if (body.errors !== undefined) this.errors = body.errors;
  }
}

// Who a request is from. A person request goes to apiOrigin() unless it names another origin; a guest request always
// names the origin it is a guest of (the web passes '', its own origin).
export type RequestOptions = { as?: 'person'; origin?: string } | { as: 'guest'; origin: string };

// A successful response: its parsed body (undefined for a 204) and its headers.
export type ApiResponse<T> = { body: T; headers: Headers };

// GET /api/me is how a host learns whether anyone is signed in. Its 401 or 403 is an answer for the caller, not a
// session that ended, so it never reaches the handlers.
const SESSION_CHECK_PATH = '/api/me';
const PENDING_APPROVAL_ERROR = 'pending_approval';
const SESSION_ENDED_ERROR = 'session_ended';

// The request with its response headers, for the few callers that read one (getJoin reads the issued guest id).
export async function requestWithHeaders<T>(
  method: string,
  path: string,
  body?: unknown,
  options: RequestOptions = {},
): Promise<ApiResponse<T>> {
  const host = platform();
  const guest = options.as === 'guest';
  const origin = options.origin ?? host.apiOrigin();
  const response = await fetch(origin + path, {
    method,
    credentials: host.credentials(),
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(guest ? host.guestHeaders(origin) : host.authHeaders()),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) {
    const error = await failure(response);
    if (path !== SESSION_CHECK_PATH) {
      if (response.status === 401 && !guest) {
        host.onUnauthorized(error.code === SESSION_ENDED_ERROR ? 'session_ended' : 'unauthorized');
      } else if (response.status === 403 && error.code === PENDING_APPROVAL_ERROR) {
        host.onPendingApproval();
      }
    }
    throw error;
  }
  const parsed = response.status === 204 ? (undefined as T) : ((await response.json()) as T);
  return { body: parsed, headers: response.headers };
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  options?: RequestOptions,
): Promise<T> {
  return (await requestWithHeaders<T>(method, path, body, options)).body;
}

// A failed response as an ApiError. Its message is the body's `error`, or a generic sentence when
// the body has none or is not JSON; the body's other members ride along for callers that show them.
async function failure(response: Response): Promise<ApiError> {
  const generic = `Request failed (${response.status})`;
  let parsed: unknown;
  try {
    parsed = await response.json();
  } catch {
    return new ApiError(generic, response.status);
  }
  if (typeof parsed !== 'object' || parsed === null) return new ApiError(generic, response.status);

  const { error, message, path, errors } = parsed as Record<string, unknown>;
  const body: ApiFailureBody = {};
  if (typeof error === 'string') body.code = error;
  if (typeof message === 'string') body.detail = message;
  if (typeof path === 'string') body.path = path;
  if (Array.isArray(errors)) body.errors = errors.filter(isFieldError);
  return new ApiError(body.code ?? generic, response.status, body);
}

function isFieldError(value: unknown): value is ApiFieldError {
  if (typeof value !== 'object' || value === null) return false;
  const { path, code, message } = value as Record<string, unknown>;
  return typeof path === 'string' && typeof code === 'string' && typeof message === 'string';
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) => request<T>('GET', path, undefined, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('POST', path, body, options),
  put: <T>(path: string, body: unknown, options?: RequestOptions) =>
    request<T>('PUT', path, body, options),
  del: <T>(path: string, options?: RequestOptions) =>
    request<T>('DELETE', path, undefined, options),
};

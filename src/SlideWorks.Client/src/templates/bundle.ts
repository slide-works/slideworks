// A template as a .zip bundle (D4). The zip is the request body itself, sent untouched: the browser
// never opens it. The server reads it into a draft and answers 201 { id }; a refusal is
// { error, message, path?, errors? } (400, 413) or { error: sentence } (415), kept on the ApiError, the
// file findings in `errors` included. The upload goes by fetch, not the api client, so a 401 and a 403
// pending_approval are answered here with a sentence: the person keeps the page and can retry.
import { ApiError, type ApiFailureBody, type ApiFieldError } from '../api/client';
import type { TemplateCreated } from './types';

const IMPORT = '/api/templates/import';
const SESSION_ENDED = 'Your session has ended. Sign in again, then upload the bundle.';
const PENDING_APPROVAL = 'pending_approval';
const PENDING_SENTENCE = 'Your request is waiting for an admin to approve it.';

// GET answers the owner's template as a zip; a link to it, for an anchor with `download`.
export const bundleUrl = (id: string) => `/api/templates/${encodeURIComponent(id)}/bundle`;

// POST /api/templates/import with the file as the body. Resolves with the new draft's id.
export async function importBundle(file: File): Promise<TemplateCreated> {
  const response = await fetch(IMPORT, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/zip' },
    body: file,
  });
  if (!response.ok) throw await refusal(response);
  return (await response.json()) as TemplateCreated;
}

async function refusal(response: Response): Promise<ApiError> {
  const generic = `Request failed (${response.status})`;
  const body: ApiFailureBody = {};
  try {
    const parsed: unknown = await response.json();
    if (typeof parsed === 'object' && parsed !== null) {
      const { error, message, path, errors } = parsed as Record<string, unknown>;
      if (typeof error === 'string') body.code = error;
      if (typeof message === 'string') body.detail = message;
      if (typeof path === 'string') body.path = path;
      if (Array.isArray(errors)) body.errors = errors.filter(isFieldError);
    }
  } catch {
    // Not JSON: the generic sentence stands.
  }
  if (response.status === 401) body.detail = SESSION_ENDED;
  if (response.status === 403 && body.code === PENDING_APPROVAL) body.detail = PENDING_SENTENCE;
  return new ApiError(body.code ?? generic, response.status, body);
}

function isFieldError(value: unknown): value is ApiFieldError {
  if (typeof value !== 'object' || value === null) return false;
  const { path, code, message } = value as Record<string, unknown>;
  return typeof path === 'string' && typeof code === 'string' && typeof message === 'string';
}

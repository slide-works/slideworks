// A template as a .zip bundle (D4). The zip is the request body itself, sent untouched: the browser
// never opens it. The server reads it into a draft and answers 201 { id }; a refusal is
// { error, message, path? } (400, 413) or { error: sentence } (415), kept on the ApiError.
import { ApiError, type ApiFailureBody } from '../api/client';
import type { TemplateCreated } from './types';

const IMPORT = '/api/templates/import';

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
      const { error, message, path } = parsed as Record<string, unknown>;
      if (typeof error === 'string') body.code = error;
      if (typeof message === 'string') body.detail = message;
      if (typeof path === 'string') body.path = path;
    }
  } catch {
    // Not JSON: the generic sentence stands.
  }
  return new ApiError(body.code ?? generic, response.status, body);
}

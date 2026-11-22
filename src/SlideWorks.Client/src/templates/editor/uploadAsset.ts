import { ApiError, api, type ApiFieldError } from '../../api/client';
import { templateErrorCode, templateErrorMessage } from '../api';
import type { TemplateAssetKind, TemplateAssetMeta } from '../types';

// What storing a file answers (PUT /api/template-uploads/{token}): the stored asset.
export type UploadedAsset = {
  assetId: string;
  kind: TemplateAssetKind;
  fileName: string;
  contentType: string;
  bytes: number;
  meta: TemplateAssetMeta;
};

// How an upload went. `refused`: the server checked the file (or the start) and said no, with its own
// sentence in `message` and, for a custom layout, one entry per finding in `errors` (its path, code
// and sentence; empty for every other kind); the file is not stored. `failed`: the server answered with something else
// (a 404, a 500), `message` is a sentence for a person. `unreachable`: no answer at all, so the
// caller words it with the file's name. Every failure stores nothing and uses up the upload address:
// the next try starts from the first call.
export type UploadOutcome =
  | { ok: true; asset: UploadedAsset }
  | { ok: false; kind: 'refused'; message: string; code: string; errors: ApiFieldError[] }
  | { ok: false; kind: 'failed' | 'unreachable'; message: string };

type Started = { uploadUrl: string; expiresLabel: string };

const UPLOADS_PATH = '/api/template-uploads/';
const BAD_UPLOAD_URL = "The upload didn't start. Try again.";

const root = (templateId: string) => `/api/templates/${encodeURIComponent(templateId)}`;

// Uploads one file in the two calls of T6: POST /api/templates/{id}/assets with { kind, fileName,
// rightsConfirmed } for a one-time address, then PUT the file's raw bytes to it. No retry here, and
// no second upload for the template may start before this one has answered (the caller sends files
// one after another). The caller says whether the person confirmed the right to serve a font; the
// server keeps it for fonts only. A custom layout's file is one JSON document, { markup, css, slots }.
export async function uploadAsset(
  templateId: string,
  kind: TemplateAssetKind,
  file: File,
  rightsConfirmed: boolean,
): Promise<UploadOutcome> {
  try {
    const started = await api.post<Started>(`${root(templateId)}/assets`, {
      kind,
      fileName: file.name,
      rightsConfirmed,
    });
    const path = uploadPathOf(started.uploadUrl);
    if (path === null) return { ok: false, kind: 'failed', message: BAD_UPLOAD_URL };
    return { ok: true, asset: await putBytes(path, file) };
  } catch (error) {
    return failureOf(error);
  }
}

// Confirms the right to serve every font the template holds, files an agent or a bundle brought
// included (POST /api/templates/{id}/assets/font-rights, no body). Answers how many fonts it covers.
export async function confirmFontRights(templateId: string): Promise<number> {
  const answer = await api.post<{ confirmed: number }>(`${root(templateId)}/assets/font-rights`);
  return answer.confirmed;
}

function failureOf(error: unknown): UploadOutcome {
  if (!(error instanceof ApiError)) {
    return { ok: false, kind: 'unreachable', message: templateErrorMessage(error) };
  }
  const code = templateErrorCode(error);
  const message = templateErrorMessage(error);
  return code === null
    ? { ok: false, kind: 'failed', message }
    : { ok: false, kind: 'refused', message, code, errors: error.errors ?? [] };
}

// The upload address is absolute on the server's public base URL, which needn't be the page's origin
// (Vite dev, a second hostname), and the server sends no CORS headers. So the PUT goes to the
// address's path and query on the page's own origin, and only when the path is under
// /api/template-uploads/: nothing else gets the file.
function uploadPathOf(uploadUrl: string): string | null {
  let url: URL;
  try {
    url = new URL(uploadUrl, window.location.origin);
  } catch {
    return null;
  }
  return url.pathname.startsWith(UPLOADS_PATH) ? url.pathname + url.search : null;
}

// PUTs the file's raw bytes. The token in the path is the credential, so no cookie is sent; the
// server does not read the Content-Type, it checks what the file is.
async function putBytes(path: string, file: File): Promise<UploadedAsset> {
  const response = await fetch(path, {
    method: 'PUT',
    credentials: 'omit',
    headers: { 'Content-Type': file.type || 'application/octet-stream' },
    body: file,
  });
  if (!response.ok) throw await refusalOf(response);
  return (await response.json()) as UploadedAsset;
}

const isFinding = (value: unknown): value is ApiFieldError =>
  typeof value === 'object' &&
  value !== null &&
  ['path', 'code', 'message'].every(
    (key) => typeof (value as Record<string, unknown>)[key] === 'string',
  );

// A failed PUT as an ApiError: { error, message, errors? } for a refused file, { error } for the rest.
async function refusalOf(response: Response): Promise<ApiError> {
  const generic = `Request failed (${response.status})`;
  let parsed: unknown;
  try {
    parsed = await response.json();
  } catch {
    return new ApiError(generic, response.status);
  }
  if (typeof parsed !== 'object' || parsed === null) return new ApiError(generic, response.status);
  const { error, message, errors } = parsed as Record<string, unknown>;
  const code = typeof error === 'string' ? error : undefined;
  const detail = typeof message === 'string' ? message : undefined;
  const findings = Array.isArray(errors) ? errors.filter(isFinding) : [];
  return new ApiError(code ?? generic, response.status, {
    ...(code === undefined ? {} : { code }),
    ...(detail === undefined ? {} : { detail }),
    ...(findings.length === 0 ? {} : { errors: findings }),
  });
}

import { useId, useRef, useState, type ChangeEvent } from 'react';
import { api, ApiError } from '../../api/client';
import type { ImageUploadRequest, ImageUploadResult } from '../../api/types';

export type LogoFieldProps = {
  deckId: string;
  logo: { url: string } | null;
  onChange(imageId: string | null): void;
};

// The types the server's image uploads accept (K11): the same list upload_image documents. SVG is refused there,
// because an SVG can carry script.
const ACCEPTED_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/gif'];
const WRONG_TYPE = 'Choose a PNG, JPEG, WebP or GIF image.';
const LIMITS = 'PNG, JPEG, WebP or GIF, up to 10 MB.';
const UNREACHABLE = "Can't reach SlideWorks.";
const BAD_UPLOAD_URL =
  "The upload didn't start: SlideWorks gave an upload address outside /api/uploads/.";
const UPLOADS_PATH = '/api/uploads/';

// Uploads a logo the way upload_image does: POST /api/decks/{id}/images for an image id and a one-time upload URL,
// then PUT the file's bytes to that URL, then hand the id to onChange, which saves it with the deck's look. A failed
// step shows the server's error (or "Can't reach SlideWorks." when the network fails) and leaves the current logo
// as it is.
export function LogoField({ deckId, logo, onChange }: LogoFieldProps) {
  const inputId = useId();
  const hintId = useId();
  const errorId = useId();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function upload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (!ACCEPTED_TYPES.includes(file.type)) {
      setError(WRONG_TYPE);
      resetInput();
      return;
    }
    setBusy(true);
    setError(null);
    let uploaded: string | null = null;
    try {
      const body: ImageUploadRequest = { name: file.name, contentType: file.type };
      const started = await api.post<ImageUploadResult>(
        `/api/decks/${encodeURIComponent(deckId)}/images`,
        body,
      );
      const path = uploadPathOf(started.uploadUrl);
      if (path === null) {
        setError(BAD_UPLOAD_URL);
      } else {
        await putBytes(path, started.method, file);
        uploaded = started.image;
      }
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : UNREACHABLE);
    } finally {
      setBusy(false);
      resetInput();
    }
    if (uploaded !== null) onChange(uploaded);
  }

  // Clears the picked file, so picking the same file again after an error tries again.
  function resetInput() {
    if (input.current) input.current.value = '';
  }

  function remove() {
    setError(null);
    onChange(null);
  }

  return (
    <div data-testid="logo-field" className="grid gap-1.5 text-meta">
      <label htmlFor={inputId} className="font-medium text-ink">
        Logo
      </label>
      {logo ? (
        <div className="flex items-center gap-2">
          <img
            data-testid="logo-field-image"
            src={logo.url}
            alt="Deck logo"
            className="h-8 max-w-[160px] object-contain"
          />
          <button
            data-testid="logo-field-remove"
            type="button"
            className="btn-ghost"
            onClick={remove}
            disabled={busy}
          >
            Remove logo
          </button>
        </div>
      ) : null}
      <input
        ref={input}
        id={inputId}
        data-testid="logo-field-file"
        type="file"
        accept={ACCEPTED_TYPES.join(',')}
        aria-describedby={error ? `${hintId} ${errorId}` : hintId}
        aria-invalid={error ? true : undefined}
        disabled={busy}
        onChange={upload}
      />
      <p id={hintId} className="text-ink-muted">
        {logo ? `Choose a file to replace it: ${LIMITS}` : LIMITS}
      </p>
      {/* Mounted empty and filled while busy, so screen readers announce the change. */}
      <p role="status" data-testid="logo-field-busy" className="text-ink-muted">
        {busy ? 'Uploading…' : ''}
      </p>
      {error ? (
        <p id={errorId} role="alert" data-testid="logo-field-error" className="text-bad">
          {error}
        </p>
      ) : null}
    </div>
  );
}

// The upload URL is absolute on the server's public base URL, which needn't be the page's origin (Vite dev, a second
// hostname), and the server sends no CORS headers. So the PUT goes to the URL's path and query on the page's own
// origin, and only when that path is under /api/uploads/: nothing else gets the file.
function uploadPathOf(uploadUrl: string): string | null {
  let url: URL;
  try {
    url = new URL(uploadUrl, window.location.origin);
  } catch {
    return null;
  }
  return url.pathname.startsWith(UPLOADS_PATH) ? url.pathname + url.search : null;
}

// PUTs the file's raw bytes to the one-time upload path. The token in it is the credential, so no cookie is sent.
async function putBytes(path: string, method: string, file: File): Promise<void> {
  const response = await fetch(path, {
    method,
    credentials: 'omit',
    headers: { 'Content-Type': file.type },
    body: file,
  });
  if (!response.ok) throw new ApiError(await errorOf(response), response.status);
}

// The upload endpoint answers a failure with { error } (400, 404, 413), as the rest of the API does.
async function errorOf(response: Response): Promise<string> {
  try {
    const parsed = (await response.json()) as { error?: string };
    return parsed.error ?? `Upload failed (${response.status})`;
  } catch {
    return `Upload failed (${response.status})`;
  }
}

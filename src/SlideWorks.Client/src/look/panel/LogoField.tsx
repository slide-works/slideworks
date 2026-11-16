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

// Uploads a logo the way upload_image does: POST /api/decks/{id}/images for an image id and a one-time upload URL,
// then PUT the file's bytes to that URL, then hand the id to onChange, which saves it with the deck's look. A failed
// step shows the server's error and leaves the current logo as it is.
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
    try {
      const body: ImageUploadRequest = { name: file.name, contentType: file.type };
      const started = await api.post<ImageUploadResult>(`/api/decks/${deckId}/images`, body);
      await putBytes(started, file);
      onChange(started.image);
    } catch (failure) {
      if (!(failure instanceof ApiError)) throw failure;
      setError(failure.message);
    } finally {
      setBusy(false);
      resetInput();
    }
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
        {logo ? 'Choose a file to replace it.' : 'PNG, JPEG, WebP or GIF, up to 10 MB.'}
      </p>
      {busy ? (
        <p role="status" data-testid="logo-field-busy" className="text-ink-muted">
          Uploading…
        </p>
      ) : null}
      {error ? (
        <p id={errorId} role="alert" data-testid="logo-field-error" className="text-bad">
          {error}
        </p>
      ) : null}
    </div>
  );
}

// PUTs the file's raw bytes to the one-time upload URL. The token in the URL is the credential, so no cookie is sent.
async function putBytes(started: ImageUploadResult, file: File): Promise<void> {
  const response = await fetch(started.uploadUrl, {
    method: started.method,
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

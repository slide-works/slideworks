import { useEffect, useRef, useState } from 'react';
import { Avatar } from '../../components/Avatar';
import type { ProfileIdentity, ProfileView } from '../../api/profile';

const UNREACHABLE = "Can't reach SlideWorks";

// The photo part of the Identity panel (D28): upload and remove are their own calls (POST and DELETE /api/me/photo) and
// take effect at once, never waiting for the panel's Save. The line under the photo says which picture is showing, and a
// refusal shows under it in the server's words. The raw bytes go as the body, with the file's type as Content-Type.
export function PhotoControl({ identity }: { identity: ProfileIdentity }) {
  const [shown, setShown] = useState({ source: identity, from: identity });
  if (shown.from !== identity) {
    setShown({ source: identity, from: identity });
  }
  const current = shown.source;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const input = useRef<HTMLInputElement>(null);
  const choose = useRef<HTMLButtonElement>(null);
  const [refocus, setRefocus] = useState(0);
  // Focus returns to the choose button once the buttons are enabled again, since a disabled button cannot take focus.
  useEffect(() => {
    if (refocus > 0 && !busy) choose.current?.focus();
  }, [refocus, busy]);

  const apply = async (send: () => Promise<Response>) => {
    setBusy(true);
    setError(null);
    try {
      const response = await send();
      if (!response.ok) {
        setError(await refusal(response));
        return false;
      }
      const profile = (await response.json()) as ProfileView;
      setShown({ source: profile.identity, from: identity });
      return true;
    } catch {
      setError(UNREACHABLE);
      return false;
    } finally {
      setBusy(false);
    }
  };

  const upload = async (file: File) => {
    await apply(() =>
      fetch('/api/me/photo', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': file.type },
        body: file,
      }),
    );
    setRefocus((n) => n + 1);
  };

  const remove = async () => {
    if (await apply(() => fetch('/api/me/photo', { method: 'DELETE', credentials: 'same-origin' }))) {
      setRefocus((n) => n + 1);
    }
  };

  let picture = 'Initials';
  if (current.hasPhoto) {
    picture = 'Your photo';
  } else if (current.avatar.pictureUrl !== null) {
    picture = 'Your Google picture';
  }
  return (
    <div className="grid gap-2">
      <div className="flex items-center gap-3">
        <Avatar avatar={current.avatar} size="lg" testId="profile-photo-avatar" />
        <button
          ref={choose}
          type="button"
          disabled={busy}
          onClick={() => input.current?.click()}
          className="btn-ghost"
        >
          {current.hasPhoto ? 'Replace photo' : 'Upload photo'}
        </button>
        {current.hasPhoto && (
          <button
            type="button"
            data-testid="profile-photo-remove"
            disabled={busy}
            onClick={remove}
            className="btn-ghost"
          >
            Remove photo
          </button>
        )}
        <input
          ref={input}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          data-testid="profile-photo-input"
          aria-label="Choose a photo"
          className="sr-only"
          tabIndex={-1}
          onChange={(event) => {
            const file = event.target.files?.[0];
            event.target.value = '';
            if (file) void upload(file);
          }}
        />
      </div>
      <p data-testid="profile-photo-source" className="text-meta text-ink-muted">
        Picture: {picture}
      </p>
      {error !== null && (
        <p role="alert" data-testid="profile-photo-error" className="text-meta text-bad">
          {error}
        </p>
      )}
    </div>
  );
}

async function refusal(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { error?: unknown };
    if (typeof body.error === 'string') return body.error;
  } catch {
    // not JSON: fall through to the generic sentence
  }
  return `Request failed (${response.status})`;
}

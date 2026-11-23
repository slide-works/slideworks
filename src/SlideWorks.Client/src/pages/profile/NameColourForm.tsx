import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, api } from '../../api/client';
import type { ProfileIdentity, ProfileView } from '../../api/profile';
import { Avatar } from '../../components/Avatar';
import { announceProfileSaved } from '../../components/UserMenu';

function colourName(key: string): string {
  return key.charAt(0).toUpperCase() + key.slice(1);
}

// The Identity panel's name and colour (D28): both wait for Save (PUT /api/me/profile); Cancel puts back the saved
// values. The swatches preview the person's own initials. An error from the server sits under the name.
export function NameColourForm({ identity }: { identity: ProfileIdentity }) {
  const [saved, setSaved] = useState(identity);
  const savedColour = saved.colour ?? saved.avatar.colour;
  const [name, setName] = useState(identity.name);
  const [colour, setColour] = useState(savedColour);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const nameRef = useRef<HTMLInputElement>(null);

  const changed = name !== saved.name || colour !== savedColour;

  async function save(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const profile = await api.put<ProfileView>('/api/me/profile', { name, colour });
      setSaved(profile.identity);
      setName(profile.identity.name);
      setColour(profile.identity.colour ?? profile.identity.avatar.colour);
      announceProfileSaved(profile.identity.name, profile.identity.avatar);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : "Can't reach SlideWorks");
    } finally {
      setSaving(false);
      // Save is disabled again after a save and the failed field is the name: either way focus goes back to it.
      nameRef.current?.focus();
    }
  }

  function cancel() {
    setName(saved.name);
    setColour(savedColour);
    setError(null);
    nameRef.current?.focus();
  }

  return (
    <form data-testid="profile-identity" onSubmit={save} className="grid gap-3">
      <div className="flex items-center gap-4">
        <Avatar avatar={saved.avatar} size="lg" testId="profile-avatar" />
        <div className="grid">
          <span data-testid="profile-name" className="text-sm font-semibold text-ink">
            {saved.name}
          </span>
          <span data-testid="profile-email" className="text-meta text-ink-muted">
            {saved.email}
          </span>
        </div>
      </div>
      <div className="grid gap-1">
        <label htmlFor="profile-name-input" className="text-meta font-medium text-ink">
          Display name
        </label>
        <input
          id="profile-name-input"
          ref={nameRef}
          data-testid="profile-name-input"
          value={name}
          onChange={(event) => setName(event.target.value)}
          aria-invalid={error !== null}
          aria-describedby={error === null ? undefined : 'profile-name-error'}
          autoComplete="name"
          className="rounded-md border border-edge bg-panel px-2.5 py-1.5 text-sm text-ink"
        />
        {error !== null && (
          <p
            id="profile-name-error"
            role="alert"
            data-testid="profile-name-error"
            className="text-meta text-bad"
          >
            {error}
          </p>
        )}
      </div>
      <div role="radiogroup" aria-label="Avatar colour" className="flex flex-wrap gap-2">
        {saved.colours.map((key) => (
          <button
            key={key}
            type="button"
            role="radio"
            aria-checked={colour === key}
            aria-label={colourName(key)}
            title={colourName(key)}
            data-testid={`profile-colour-${key}`}
            onClick={() => setColour(key)}
            className={`rounded-full p-0.5 ${colour === key ? 'ring-2 ring-accent' : 'ring-1 ring-edge'}`}
          >
            <Avatar
              avatar={{ initials: saved.avatar.initials, colour: key, pictureUrl: null }}
              testId={`profile-swatch-${key}`}
            />
          </button>
        ))}
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          data-testid="profile-save"
          disabled={!changed || saving}
          className="btn-primary"
        >
          Save
        </button>
        <button
          type="button"
          data-testid="profile-cancel"
          disabled={!changed || saving}
          onClick={cancel}
          className="btn-ghost"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

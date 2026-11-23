import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, api } from '../../api/client';
import type { ProfileAccount, ProfileView } from '../../api/profile';

// The time zone of the Account panel: a select of the server's zones that saves with Save (PUT /api/me/time-zone).
// An empty choice means "not set", which shows dates in UTC. An error from the server sits under the select.
export function TimeZoneField({ account }: { account: ProfileAccount }) {
  const [saved, setSaved] = useState(account.timeZone);
  const [choice, setChoice] = useState(account.timeZone ?? '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const selectRef = useRef<HTMLSelectElement>(null);

  const changed = choice !== (saved ?? '') && choice !== '';

  async function save(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const profile = await api.put<ProfileView>('/api/me/time-zone', {
        timeZone: choice,
        fromBrowser: false,
      });
      setSaved(profile.account.timeZone);
      setChoice(profile.account.timeZone ?? '');
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : "Can't reach SlideWorks");
    } finally {
      setSaving(false);
      selectRef.current?.focus();
    }
  }

  return (
    <form data-testid="profile-time-zone" onSubmit={save} className="grid gap-1">
      <label htmlFor="profile-time-zone-input" className="text-meta font-medium text-ink">
        Time zone
      </label>
      <div className="flex gap-2">
        <select
          id="profile-time-zone-input"
          ref={selectRef}
          data-testid="profile-time-zone-select"
          value={choice}
          onChange={(event) => setChoice(event.target.value)}
          aria-invalid={error !== null}
          aria-describedby={error === null ? 'profile-time-zone-hint' : 'profile-time-zone-hint profile-time-zone-error'}
          className="min-w-0 flex-1 rounded-md border border-edge bg-panel px-2.5 py-1.5 text-sm text-ink"
        >
          {saved === null && <option value="">Not set (UTC)</option>}
          {account.timeZones.map((zone) => (
            <option key={zone.id} value={zone.id}>
              {zone.label}
            </option>
          ))}
        </select>
        <button
          type="submit"
          data-testid="profile-time-zone-save"
          aria-label="Save time zone"
          disabled={!changed || saving}
          className="btn-primary"
        >
          Save
        </button>
      </div>
      <p id="profile-time-zone-hint" className="text-meta text-ink-muted">
        Dates in change logs and lists use this zone. Slide footers stay in UTC.
      </p>
      {error !== null && (
        <p
          id="profile-time-zone-error"
          role="alert"
          data-testid="profile-time-zone-error"
          className="text-meta text-bad"
        >
          {error}
        </p>
      )}
    </form>
  );
}

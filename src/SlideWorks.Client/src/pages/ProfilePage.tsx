import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { ProfileView } from '../api/profile';
import { AgentsPanel } from './profile/AgentsPanel';
import { NameColourForm } from './profile/NameColourForm';
import { PhotoControl } from './profile/PhotoControl';
import { TimeZoneField } from './profile/TimeZoneField';

type State = { kind: 'loading' } | { kind: 'loaded'; profile: ProfileView } | { kind: 'failed'; message: string };

const UNREACHABLE = "Can't reach SlideWorks";

// /profile: the signed-in person's profile, from one GET /api/me/profile. Three panels: Identity (name, colour,
// photo), Account (time zone, access, the admin link) and Connected agents. The Account panel's access and admin link
// are read-only facts.
export function ProfilePage() {
  const [state, setState] = useState<State>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    api.get<ProfileView>('/api/me/profile').then(
      (profile) => active && setState({ kind: 'loaded', profile }),
      (error: unknown) =>
        active &&
        setState({ kind: 'failed', message: error instanceof ApiError ? error.message : UNREACHABLE }),
    );
    return () => {
      active = false;
    };
  }, []);

  if (state.kind === 'failed') {
    return (
      <section data-testid="profile-page" className="grid gap-3">
        <h2 className="text-lg font-bold tracking-tight text-ink">Profile</h2>
        <p role="alert" data-testid="profile-error" className="panel px-4 py-3 text-meta text-bad">
          {state.message}
        </p>
      </section>
    );
  }
  const profile = state.kind === 'loaded' ? state.profile : null;
  return (
    <section data-testid="profile-page" className="grid max-w-2xl gap-5">
      <h2 className="text-lg font-bold tracking-tight text-ink">Profile</h2>
      {profile && (
        <>
          <section data-testid="profile-identity-panel" aria-labelledby="profile-identity-title" className="panel grid gap-3 p-4">
            <h3 id="profile-identity-title" className="text-sm font-bold text-ink">
              Identity
            </h3>
            <NameColourForm identity={profile.identity} />
            <PhotoControl identity={profile.identity} />
          </section>
          <section data-testid="profile-account-panel" aria-labelledby="profile-account-title" className="panel grid gap-3 p-4">
            <h3 id="profile-account-title" className="text-sm font-bold text-ink">
              Account
            </h3>
            <TimeZoneField account={profile.account} />
            <p data-testid="profile-access" className="text-meta text-ink-muted">
              Access: {profile.account.isAdmin ? 'Admin' : 'Member'}
            </p>
            {profile.account.isAdmin && (
              <Link
                to={profile.account.adminUrl}
                data-testid="profile-admin-link"
                className="text-meta font-medium text-accent"
              >
                Manage people
              </Link>
            )}
          </section>
          <AgentsPanel agents={profile.agents} />
        </>
      )}
    </section>
  );
}

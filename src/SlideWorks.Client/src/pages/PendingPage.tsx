import { useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { MeView } from '../api/types';

type State =
  | { kind: 'checking' }
  | { kind: 'signedOut' }
  | { kind: 'approved' }
  | { kind: 'unreachable' }
  | { kind: 'pending'; me: MeView };

// /pending, where a signed-in person waits for an admin to approve them. It sits outside the App gate, so it reads
// GET /api/me itself: signed out goes to /signin, approved goes to /decks.
export function PendingPage() {
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'checking' });

  useEffect(() => {
    let active = true;
    api.get<MeView>('/api/me').then(
      (me) =>
        active &&
        setState(me.access === 'approved' ? { kind: 'approved' } : { kind: 'pending', me }),
      (error: unknown) =>
        active &&
        setState({
          kind: error instanceof ApiError && error.status === 401 ? 'signedOut' : 'unreachable',
        }),
    );
    return () => {
      active = false;
    };
  }, []);

  const signOut = async () => {
    await api.post('/api/signout');
    navigate('/signin');
  };

  switch (state.kind) {
    case 'checking':
      return null;
    case 'signedOut':
      return <Navigate to="/signin" replace />;
    case 'approved':
      return <Navigate to="/decks" replace />;
    case 'unreachable':
      return (
        <main data-testid="pending-page" className="mx-auto max-w-md px-4 py-16 text-center">
          <h1 data-testid="pending-unreachable" className="page-title">
            Can't reach SlideWorks
          </h1>
          <p className="text-meta text-ink-muted">Check your connection, then reload the page.</p>
        </main>
      );
    case 'pending':
      return (
        <main data-testid="pending-page" className="mx-auto grid max-w-sm gap-4 px-4 py-16">
          <h1 data-testid="pending-heading" className="page-title">
            Waiting for approval
          </h1>
          <p className="text-meta text-ink-muted">
            Your request is waiting for an admin to approve it.
          </p>
          <p data-testid="pending-email" className="text-meta text-ink">
            {state.me.email}
          </p>
          {state.me.adminMissing && (
            <p
              data-testid="pending-admin-missing"
              className="rounded-panel border border-edge-strong bg-panel-2 px-3 py-2 text-meta text-ink"
            >
              This server has no admin yet: set SLIDEWORKS_ADMIN_EMAIL.
            </p>
          )}
          <button
            type="button"
            className="btn-primary"
            data-testid="pending-sign-out"
            onClick={() => void signOut()}
          >
            Sign out
          </button>
        </main>
      );
  }
}

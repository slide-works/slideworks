import { useEffect, useState, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError, api } from './api/client';
import type { MeView } from './api/types';

type Session =
  | { kind: 'checking' }
  | { kind: 'signedIn'; me: MeView }
  | { kind: 'signedOut' }
  | { kind: 'unreachable' };

// The auth gate for every page that needs an approved, signed-in person: the Shell pages (App) and the full-screen
// presenter and phone pages. GET /api/me decides: a 401 sends the user to /signin with the whole location (path, query
// and hash) as returnTo, a pending person goes to /pending, and any other failure means the server can't be reached.
// children renders the page for the signed-in person.
export function SignedInGate({ children }: { children: (me: MeView) => ReactNode }) {
  const location = useLocation();
  const [session, setSession] = useState<Session>({ kind: 'checking' });

  useEffect(() => {
    let active = true;
    api.get<MeView>('/api/me').then(
      (me) => active && setSession({ kind: 'signedIn', me }),
      (error: unknown) =>
        active &&
        setSession({
          kind: error instanceof ApiError && error.status === 401 ? 'signedOut' : 'unreachable',
        }),
    );
    return () => {
      active = false;
    };
  }, []);

  switch (session.kind) {
    case 'checking':
      return null;
    case 'signedOut': {
      const returnTo = location.pathname + location.search + location.hash;
      return <Navigate to={`/signin?returnTo=${encodeURIComponent(returnTo)}`} replace />;
    }
    case 'unreachable':
      return (
        <main data-testid="app-unreachable" className="mx-auto max-w-md px-4 py-16 text-center">
          <h1 className="page-title">Can't reach SlideWorks</h1>
          <p className="text-meta text-ink-muted">Check your connection, then reload the page.</p>
        </main>
      );
    case 'signedIn':
      if (session.me.access === 'pending') return <Navigate to="/pending" replace />;
      return children(session.me);
  }
}

import { useEffect, useState, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError, api } from './api/client';
import type { MeView } from './api/types';
import { DEVICE_SIGN_IN_TEST_IDS } from './pages/deviceSignIn/testIds';

type Session =
  | { kind: 'checking' }
  | { kind: 'signedIn'; me: MeView }
  | { kind: 'signedOut' }
  | { kind: 'unreachable' };

// The gate for /app/sign-in (contract K6), which any signed-in person may use, pending included: SignedInGate would
// send a pending person to /pending, so this page has its own. GET /api/me decides: a 401 sends the person to /signin
// with the whole location (path, query and hash) as returnTo, so they come back to the same confirm page; any other
// failure means the server can't be reached. children renders the page for the signed-in person.
export function DeviceSignInGate({ children }: { children: (me: MeView) => ReactNode }) {
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
        <main
          data-testid={DEVICE_SIGN_IN_TEST_IDS.unreachable}
          className="mx-auto max-w-md px-4 py-16 text-center"
        >
          <h1 className="page-title">Can't reach SlideWorks</h1>
          <p className="text-meta text-ink-muted">Check your connection, then reload the page.</p>
        </main>
      );
    case 'signedIn':
      return children(session.me);
  }
}

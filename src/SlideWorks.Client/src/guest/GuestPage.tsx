import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import { GuestLive } from './GuestLive';
import { GuestOffline } from './GuestOffline';
import { GuestShell } from './GuestShell';
import { NO_SESSION, TOO_MANY_ATTEMPTS, type GuestView } from './guestView';
import { JoinCodePage } from './JoinCodePage';
import { GUEST_TEST_IDS } from './testIds';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: GuestView }
  | { kind: 'rejected'; error: 'no_session' | 'too_many_attempts' }
  | { kind: 'failed' };

// /j/:code and /:code, open to anyone with no sign-in. It reads GET /api/join/{code} once; GuestScreens switches on the
// phase and status that useAudience reports from then on (GuestLive).
export function GuestPage() {
  const { code = '' } = useParams();
  const [result, setResult] = useState<{ code: string; load: Load } | null>(null);
  const [attempt, setAttempt] = useState(0);
  const load: Load = result?.code === code ? result.load : { kind: 'loading' };

  useEffect(() => {
    let current = true;
    const show = (next: Load) => current && setResult({ code, load: next });
    api.get<GuestView>(`/api/join/${encodeURIComponent(code)}`).then(
      (view) => show({ kind: 'loaded', view }),
      (error: unknown) => {
        if (error instanceof ApiError && error.message === NO_SESSION)
          show({ kind: 'rejected', error: NO_SESSION });
        else if (error instanceof ApiError && error.message === TOO_MANY_ATTEMPTS)
          show({ kind: 'rejected', error: TOO_MANY_ATTEMPTS });
        else show({ kind: 'failed' });
      },
    );
    return () => {
      current = false;
    };
  }, [code, attempt]);

  const retry = useCallback(() => setAttempt((n) => n + 1), []);

  return (
    <div data-testid={GUEST_TEST_IDS.page}>
      {load.kind === 'loading' && <p data-testid={GUEST_TEST_IDS.loading}>Loading</p>}
      {load.kind === 'rejected' && <JoinCodePage code={code} error={load.error} />}
      {load.kind === 'failed' && (
        <GuestShell>
          <GuestOffline onRetry={retry} />
        </GuestShell>
      )}
      {load.kind === 'loaded' && <GuestLive code={code} view={load.view} onRetry={retry} />}
    </div>
  );
}

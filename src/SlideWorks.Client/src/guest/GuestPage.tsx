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
  const [attempt, setAttempt] = useState({ code, n: 0 });
  const retries = attempt.code === code ? attempt.n : 0;
  const load: Load = result?.code === code ? result.load : { kind: 'loading' };

  // One read of the view. Only the first read for a code, from the keypad path, may end on the keypad's "wait a minute"
  // message: a retry that is rate limited leaves the screen as it is (the offline screen keeps backing off).
  const read = useCallback(
    (isRetry: boolean, live: () => boolean) => {
      const show = (next: Load) => live() && setResult({ code, load: next });
      return api.get<GuestView>(`/api/join/${encodeURIComponent(code)}`).then(
        (view) => show({ kind: 'loaded', view }),
        (error: unknown) => {
          if (error instanceof ApiError && error.message === NO_SESSION)
            show({ kind: 'rejected', error: NO_SESSION });
          else if (error instanceof ApiError && error.message === TOO_MANY_ATTEMPTS) {
            if (!isRetry) show({ kind: 'rejected', error: TOO_MANY_ATTEMPTS });
          } else show({ kind: 'failed' });
        },
      );
    },
    [code],
  );

  useEffect(() => {
    let current = true;
    void read(retries > 0, () => current);
    return () => {
      current = false;
    };
  }, [read, retries]);

  const retry = useCallback(() => setAttempt({ code, n: retries + 1 }), [code, retries]);
  // The offline screen's own retries read here, so it can wait for each one to settle before the next.
  const retryOffline = useCallback(() => read(true, () => true), [read]);

  return (
    <div data-testid={GUEST_TEST_IDS.page}>
      {load.kind === 'loading' && <p data-testid={GUEST_TEST_IDS.loading}>Loading</p>}
      {load.kind === 'rejected' && <JoinCodePage code={code} error={load.error} />}
      {load.kind === 'failed' && (
        <GuestShell>
          <GuestOffline onRetry={retryOffline} />
        </GuestShell>
      )}
      {load.kind === 'loaded' && <GuestLive code={code} view={load.view} onRetry={retry} />}
    </div>
  );
}

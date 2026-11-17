import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import { GuestLive } from './GuestLive';
import { GuestOffline } from './GuestOffline';
import { GuestShell } from './GuestShell';
import { NO_SESSION, TOO_MANY_ATTEMPTS, type GuestView } from './guestView';
import { JoinCodePage } from './JoinCodePage';
import { GUEST_TEST_IDS } from './testIds';
import type { RetryOutcome } from './useRetry';

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
  // The code the page is on now, for a read still in flight to ask whether its answer is still wanted.
  const currentCode = useRef(code);
  useEffect(() => {
    currentCode.current = code;
  }, [code]);
  const load: Load = result?.code === code ? result.load : { kind: 'loading' };

  // One read of the view. Only the first read for a code, from the keypad path, may end on the keypad's "wait a minute"
  // message: a retry that is rate limited leaves the screen as it is, and reports 'throttled' so the button that asked can
  // say so (the offline screen keeps backing off).
  const read = useCallback(
    (isRetry: boolean, live: () => boolean): Promise<RetryOutcome> => {
      const show = (next: Load) => live() && setResult({ code, load: next });
      return api.get<GuestView>(`/api/join/${encodeURIComponent(code)}`).then(
        (view): RetryOutcome => {
          show({ kind: 'loaded', view });
          return 'done';
        },
        (error: unknown): RetryOutcome => {
          if (error instanceof ApiError && error.message === NO_SESSION)
            show({ kind: 'rejected', error: NO_SESSION });
          else if (error instanceof ApiError && error.message === TOO_MANY_ATTEMPTS) {
            if (!isRetry) show({ kind: 'rejected', error: TOO_MANY_ATTEMPTS });
            else return 'throttled';
          } else show({ kind: 'failed' });
          return 'done';
        },
      );
    },
    [code],
  );

  useEffect(() => {
    let current = true;
    void read(false, () => current);
    return () => {
      current = false;
    };
  }, [read]);

  // Every retry (the offline screen's, the cookies screen's, the error screen's) reads here and returns its promise, so
  // the caller can wait for it. A retry still in flight when the code in the URL changes must not touch the new code's page.
  const retry = useCallback(() => read(true, () => currentCode.current === code), [read, code]);

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

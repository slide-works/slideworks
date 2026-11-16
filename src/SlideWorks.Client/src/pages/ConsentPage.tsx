import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import type { ConsentDecisionView, ConsentView } from '../api/types';

const GONE_MESSAGE =
  'This request has expired or was already answered. Go back to your agent and connect again.';

type State =
  | { kind: 'loading' }
  | { kind: 'ready'; consent: ConsentView; answering: boolean }
  | { kind: 'gone' }
  | { kind: 'failed'; message: string };

// /oauth/consent?request=<id>: a signed-in presenter allows or denies an agent. The answer
// comes back as the agent's redirect URI (with a code, or access_denied), and the browser goes
// there. An expired, answered or someone else's request (410) shows a message instead.
export function ConsentPage() {
  const [params] = useSearchParams();
  const requestId = params.get('request') ?? '';
  const [loaded, setState] = useState<State>({ kind: 'loading' });
  const state: State = requestId === '' ? { kind: 'gone' } : loaded;
  const path = `/api/oauth/consent/${encodeURIComponent(requestId)}`;

  useEffect(() => {
    let active = true;
    if (requestId === '') return;
    api.get<ConsentView>(path).then(
      (consent) => active && setState({ kind: 'ready', consent, answering: false }),
      (error: unknown) => active && setState(failure(error)),
    );
    return () => {
      active = false;
    };
  }, [path, requestId]);

  async function answer(approve: boolean) {
    if (state.kind !== 'ready') return;
    setState({ ...state, answering: true });
    try {
      const decision = await api.post<ConsentDecisionView>(path, { approve });
      window.location.assign(decision.redirect);
    } catch (error) {
      setState(failure(error));
    }
  }

  return (
    <section data-testid="consent-page" className="mx-auto grid max-w-md gap-4 px-4 py-16">
      {state.kind === 'ready' && (
        <>
          <h1 className="page-title" data-testid="consent-title">
            Allow <span data-testid="consent-client-name">{state.consent.clientName}</span> to use
            SlideWorks as you?
          </h1>
          <p className="text-meta text-ink-muted" data-testid="consent-scope">
            It will be able to create, change and share your decks ({state.consent.scope}).
          </p>
          <p className="text-meta text-ink-muted" data-testid="consent-returns-to">
            Your answer goes back to{' '}
            <span className="font-medium text-ink" data-testid="consent-returns-to-host">
              {state.consent.returnsTo}
            </span>
            . Only allow it if that is the agent you are connecting.
          </p>
          <div className="flex gap-3">
            <button
              type="button"
              className="btn-primary"
              data-testid="consent-allow"
              disabled={state.answering}
              onClick={() => void answer(true)}
            >
              Allow
            </button>
            <button
              type="button"
              className="btn-ghost"
              data-testid="consent-deny"
              disabled={state.answering}
              onClick={() => void answer(false)}
            >
              Deny
            </button>
          </div>
        </>
      )}
      {state.kind === 'gone' && (
        <p role="alert" data-testid="consent-gone" className="text-meta text-ink">
          {GONE_MESSAGE}
        </p>
      )}
      {state.kind === 'failed' && (
        <p role="alert" data-testid="consent-failed" className="text-meta text-ink">
          {state.message}
        </p>
      )}
    </section>
  );
}

function failure(error: unknown): State {
  if (error instanceof ApiError && error.status === 410) return { kind: 'gone' };
  return {
    kind: 'failed',
    message:
      error instanceof ApiError
        ? error.message
        : 'Can’t reach SlideWorks. Check your connection, then reload the page.',
  };
}

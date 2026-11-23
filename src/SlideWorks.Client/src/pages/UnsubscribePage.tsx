import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { ApiError, api } from '../api/client';

const GENERIC_PROBLEM = 'Something went wrong, try again';

type State = 'ask' | 'sending' | 'done' | 'failed';

// /unsubscribe, where a share email's link leads, open without a sign-in. The token is in the URL fragment, so it never
// reaches a server log; nothing is sent until the one button is pressed, so a mail scanner that only opens the link
// unsubscribes nobody. The page never shows the address. Only a 400 means the link itself is bad; any other failure keeps
// the button, shows the server's text (or a generic one) and lets the person try again.
export function UnsubscribePage() {
  const { hash } = useLocation();
  const token = new URLSearchParams(hash.replace(/^#/, '')).get('t');
  const [state, setState] = useState<State>(token ? 'ask' : 'failed');
  const [problem, setProblem] = useState<string | null>(null);
  const result = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    if (state === 'done' || (state === 'failed' && token)) result.current?.focus();
  }, [state, token]);

  const stop = async () => {
    if (!token) return;
    setState('sending');
    setProblem(null);
    try {
      await api.post('/api/email/unsubscribe', { token });
      setState('done');
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setState('failed');
        return;
      }
      setProblem(error instanceof ApiError && error.code ? error.message : GENERIC_PROBLEM);
      setState('ask');
    }
  };

  return (
    <main data-testid="unsubscribe-page" className="mx-auto grid max-w-md gap-3 px-4 py-16">
      {state === 'done' && (
        <section data-testid="unsubscribe-done" className="grid gap-3">
          <h1 ref={result} tabIndex={-1} className="page-title">
            Done
          </h1>
          <p className="text-meta text-ink-muted">SlideWorks won’t email this address again.</p>
        </section>
      )}
      {state === 'failed' && (
        <section data-testid="unsubscribe-error" role="alert" className="grid gap-3">
          <h1 ref={result} tabIndex={-1} className="page-title">
            Link didn’t work
          </h1>
          <p className="text-meta text-ink-muted">
            It may be old or cut short. Use the link in your latest email.
          </p>
        </section>
      )}
      {(state === 'ask' || state === 'sending') && (
        <section data-testid="unsubscribe-ask" className="grid gap-3">
          <h1 data-testid="unsubscribe-heading" className="page-title">
            Stop emails from SlideWorks?
          </h1>
          <p className="text-meta text-ink-muted">
            We won’t email this address when someone shares a deck with it. Decks shared with you
            still show under Shared with you.
          </p>
          {problem && (
            <p role="alert" data-testid="unsubscribe-problem" className="text-meta text-ink">
              {problem}
            </p>
          )}
          <button
            type="button"
            className="btn-primary justify-self-start"
            data-testid="unsubscribe-stop"
            disabled={state === 'sending'}
            onClick={() => void stop()}
          >
            {problem ? 'Try again' : 'Stop emails'}
          </button>
        </section>
      )}
    </main>
  );
}

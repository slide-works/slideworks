import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import type { SignInOptionsView } from '../api/types';

// The codes the server puts on /signin?error= when sign-in with Google fails or is
// refused. Anything else shows the generic message.
const ERROR_MESSAGES: Record<string, string> = {
  cancelled: 'Sign-in was cancelled. Try again when you’re ready.',
  failed: 'Sign-in didn’t work. Try again.',
  unverified: 'Google hasn’t verified this email address, so it can’t be used to sign in.',
  email_linked: 'This email is linked to another Google account.',
  not_approved: 'An admin hasn’t approved this account on this server.',
};

type Options =
  { kind: 'checking' } | { kind: 'ready'; options: SignInOptionsView } | { kind: 'unreachable' };

// /signin?returnTo=<local path + query + hash>&error=<code>. Open to everyone.
// The button is a plain link to the server's /signin/google, a full-page
// navigation to Google and back, carrying returnTo through; the server checks
// that it is a local path.
export function SignInPage() {
  const [params] = useSearchParams();
  const [state, setState] = useState<Options>({ kind: 'checking' });
  const error = params.get('error');
  const returnTo = params.get('returnTo') ?? '/';

  useEffect(() => {
    let active = true;
    api.get<SignInOptionsView>('/api/signin/options').then(
      (options) => active && setState({ kind: 'ready', options }),
      () => active && setState({ kind: 'unreachable' }),
    );
    return () => {
      active = false;
    };
  }, []);

  return (
    <main data-testid="signin-page" className="mx-auto grid max-w-sm gap-4 px-4 py-16">
      <h1 className="page-title">Sign in to SlideWorks</h1>
      <p className="text-meta text-ink-muted">
        Sign in to see your decks and the decks shared with you.
      </p>
      {error !== null && (
        <p
          role="alert"
          data-testid="signin-error"
          className="rounded-panel border border-edge-strong bg-panel-2 px-3 py-2 text-meta text-ink"
        >
          {ERROR_MESSAGES[error] ?? ERROR_MESSAGES.failed}
        </p>
      )}
      {state.kind === 'ready' && state.options.google && (
        <>
          <a
            href={`/signin/google?returnTo=${encodeURIComponent(returnTo)}`}
            className="btn-primary no-underline"
            data-testid="signin-google"
          >
            Sign in with Google
          </a>
          <p data-testid="signin-request-hint" className="text-meta text-ink-muted">
            New here? Sign in with Google to request access.
          </p>
        </>
      )}
      {state.kind === 'ready' && !state.options.google && (
        <p data-testid="signin-not-set-up" className="text-meta text-ink-muted">
          Sign-in isn’t set up on this server.
        </p>
      )}
      {state.kind === 'unreachable' && (
        <p data-testid="signin-unreachable" className="text-meta text-ink-muted">
          Can’t reach SlideWorks. Check your connection, then reload the page.
        </p>
      )}
    </main>
  );
}

// /signin?returnTo=<local path + query + hash>. Open to everyone.
// TODO: shell with an inert button; Google sign-in (which carries returnTo
// through) replaces it.
export function SignInPage() {
  return (
    <main data-testid="signin-page" className="mx-auto grid max-w-sm gap-4 px-4 py-16">
      <h1 className="page-title">Sign in to SlideWorks</h1>
      <p className="text-meta text-ink-muted">
        Sign in to see your decks and the decks shared with you.
      </p>
      <button type="button" className="btn-primary" data-testid="signin-google" disabled>
        Sign in with Google
      </button>
    </main>
  );
}

import { Link } from 'react-router-dom';

type PlayerMessageProps = {
  testId: string;
  title: string;
  body: string | null;
  // Where "Sign in" goes, or null when it isn't offered.
  signInTo: string | null;
};

// What the player page shows instead of a deck: not found, or the server
// can't be reached.
export function PlayerMessage({ testId, title, body, signInTo }: PlayerMessageProps) {
  return (
    <main
      data-testid="player-page"
      className="grid min-h-dvh place-content-center gap-2 px-4 text-center"
    >
      <h1 data-testid={testId} className="page-title">
        {title}
      </h1>
      {body !== null && <p className="text-meta text-ink-muted">{body}</p>}
      {signInTo !== null && (
        <Link
          data-testid="player-sign-in"
          to={signInTo}
          className="btn-primary mx-auto mt-2 no-underline"
        >
          Sign in
        </Link>
      )}
    </main>
  );
}

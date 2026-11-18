import sampleDeckJson from '@contracts/sample-deck.json' with { type: 'json' };
import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { DeckPlayView, MeView } from '../api/types';
import { LookPreview } from '../look/LookPreview';
import { templatesApi } from './api';
import { TEMPLATES_TEST_IDS } from './testIds';
import type { TemplatePublicView } from './types';

const sampleDeck = sampleDeckJson as DeckPlayView;
const PREVIEW_SLIDES = sampleDeck.slides.filter((slide) => [1, 8].includes(slide.number));

const USE_FAILED = 'Could not add this template. Try again.';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: TemplatePublicView }
  | { kind: 'notFound' }
  | { kind: 'failed' };

// /t/:id, the page a template's share link opens. Open to everyone: the server answers the same
// 404 for a private template and for a wrong link, and the page then says the same for both, so a
// stranger never learns a private template exists. Every string of the template is drawn as text.
export function TemplatePublicPage() {
  const { id = '' } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const useButtonRef = useRef<HTMLButtonElement>(null);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [signedOut, setSignedOut] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const adding = useRef(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    templatesApi.publicPage(id).then(
      (view) => active && setLoad({ kind: 'loaded', view }),
      (failure: unknown) =>
        active &&
        setLoad({
          kind: failure instanceof ApiError && failure.status === 404 ? 'notFound' : 'failed',
        }),
    );
    // Only to choose the button: the public view is the same for everyone.
    api.get<MeView>('/api/me').then(
      () => active && setSignedOut(false),
      (failure: unknown) =>
        active && setSignedOut(failure instanceof ApiError && failure.status === 401),
    );
    return () => {
      active = false;
    };
  }, [id, attempt]);

  useEffect(() => {
    if (load.kind !== 'loading') headingRef.current?.focus();
  }, [load.kind]);

  const signIn = () => {
    const returnTo = location.pathname + location.search + location.hash;
    void navigate(`/signin?returnTo=${encodeURIComponent(returnTo)}`, { replace: true });
  };

  const use = async () => {
    if (signedOut) {
      signIn();
      return;
    }
    if (adding.current) return;
    adding.current = true;
    setError(null);
    try {
      await templatesApi.useTemplate(id);
      void navigate(`/decks?template=${encodeURIComponent(id)}`);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        signIn();
        return;
      }
      adding.current = false;
      setError(failure instanceof ApiError && failure.detail ? failure.detail : USE_FAILED);
      useButtonRef.current?.focus();
    }
  };

  if (load.kind === 'loading') {
    return (
      <main data-testid={TEMPLATES_TEST_IDS.publicPage} className="mx-auto max-w-3xl px-4 py-10">
        <p role="status" className="text-meta text-ink-muted">
          Loading…
        </p>
      </main>
    );
  }

  if (load.kind === 'notFound' || load.kind === 'failed') {
    const missing = load.kind === 'notFound';
    return (
      <main data-testid={TEMPLATES_TEST_IDS.publicPage} className="mx-auto max-w-3xl px-4 py-10">
        <h1
          ref={headingRef}
          tabIndex={-1}
          data-testid="template-public-message"
          className="text-lg font-bold tracking-tight text-ink outline-none"
        >
          {missing
            ? 'This template is private, or the link is wrong.'
            : 'Could not load this template.'}
        </h1>
        <div className="mt-4">
          {missing ? (
            <Link
              to="/templates"
              data-testid="template-public-templates"
              className="btn-ghost no-underline"
            >
              Go to your templates
            </Link>
          ) : (
            <button
              type="button"
              data-testid="template-public-retry"
              className="btn-ghost"
              onClick={() => {
                setLoad({ kind: 'loading' });
                setAttempt((count) => count + 1);
              }}
            >
              Retry
            </button>
          )}
        </div>
      </main>
    );
  }

  const { view } = load;
  return (
    <main
      data-testid={TEMPLATES_TEST_IDS.publicPage}
      className="mx-auto grid max-w-3xl gap-4 px-4 py-10"
    >
      <header className="grid gap-1">
        <h1
          ref={headingRef}
          tabIndex={-1}
          data-testid="template-public-name"
          className="text-lg font-bold tracking-tight text-ink outline-none"
        >
          {view.name}
        </h1>
        {view.owner !== '' && (
          <p data-testid="template-public-owner" className="text-meta text-ink-muted">
            By {view.owner}
          </p>
        )}
        {view.purpose !== '' && (
          <p data-testid="template-public-purpose" className="text-sm text-ink">
            {view.purpose}
          </p>
        )}
      </header>

      <figure className="grid gap-2" data-testid="template-public-preview">
        <LookPreview look={view.look} slides={PREVIEW_SLIDES} />
        <figcaption className="sr-only">{describeTemplatePreview(view)}</figcaption>
      </figure>

      <div className="grid justify-items-start gap-2">
        <button
          ref={useButtonRef}
          type="button"
          data-testid="template-public-use"
          className="btn-primary"
          onClick={() => void use()}
        >
          {signedOut ? 'Sign in to use this template' : 'Use this template'}
        </button>
        {error !== null && (
          <p role="alert" data-testid="template-public-error" className="text-meta text-bad">
            {error}
          </p>
        )}
      </div>
    </main>
  );
}

// One sentence for a screen reader about the picture of the template.
function describeTemplatePreview(view: TemplatePublicView): string {
  return `Two sample slides drawn in ${view.name}: a title slide and an exercise slide.`;
}

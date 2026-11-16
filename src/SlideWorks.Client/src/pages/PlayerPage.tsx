import { useCallback, useEffect, useState } from 'react';
import { Navigate, useLocation, useParams } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { DeckPlayView, MeView } from '../api/types';
import { Player } from '../player/Player';
import { useDeckRealtime } from '../realtime/useDeckRealtime';
import { PlayerMessage } from './deck/PlayerMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: DeckPlayView }
  // signedIn is null until GET /api/me answers, or when it can't.
  | { kind: 'notFound'; signedIn: boolean | null }
  | { kind: 'failed'; message: string };

// /d/:id/:slug (and /d/:id): the player, open to anonymous viewers, from one
// GET /api/play/{id} (DeckPlayView). A missing or old slug is replaced by the
// deck's own, keeping the hash. It re-reads when the deck changes.
export function PlayerPage() {
  const { id = '', slug } = useParams();
  const location = useLocation();
  // Each read is stamped with its deck, so another deck's result never shows.
  const [result, setResult] = useState<{ id: string; load: Load } | null>(null);
  const load: Load = result?.id === id ? result.load : { kind: 'loading' };

  const read = useCallback(
    (isActive: () => boolean = () => true) => {
      const show = (next: Load) => setResult({ id, load: next });
      return api.get<DeckPlayView>(`/api/play/${encodeURIComponent(id)}`).then(
        (view) => isActive() && show({ kind: 'loaded', view }),
        (error: unknown) => {
          if (!isActive()) return;
          if (error instanceof ApiError && error.status === 404) {
            show({ kind: 'notFound', signedIn: null });
            // Only to decide whether to offer sign-in: a private deck reads as not found when signed out.
            api.get<MeView>('/api/me').then(
              () => isActive() && show({ kind: 'notFound', signedIn: true }),
              (meError: unknown) =>
                isActive() &&
                meError instanceof ApiError &&
                meError.status === 401 &&
                show({ kind: 'notFound', signedIn: false }),
            );
          } else {
            // A re-read that fails mid-presentation keeps the deck already on screen.
            setResult((previous) =>
              previous?.id === id && previous.load.kind === 'loaded'
                ? previous
                : {
                    id,
                    load: {
                      kind: 'failed',
                      message:
                        error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
                    },
                  },
            );
          }
        },
      );
    },
    [id],
  );

  useEffect(() => {
    let active = true;
    void read(() => active);
    return () => {
      active = false;
    };
  }, [read]);

  useDeckRealtime(id, () => void read());

  switch (load.kind) {
    case 'loading':
      return <div data-testid="player-page" data-deck-id={id} className="h-dvh bg-[#05070d]" />;
    case 'notFound': {
      const returnTo = location.pathname + location.search + location.hash;
      return (
        <PlayerMessage
          testId="player-not-found"
          title="Deck not found"
          body="It may be private, or the link may be wrong."
          signInTo={
            load.signedIn === false ? `/signin?returnTo=${encodeURIComponent(returnTo)}` : null
          }
        />
      );
    }
    case 'failed':
      return (
        <PlayerMessage testId="player-error" title={load.message} body={null} signInTo={null} />
      );
    case 'loaded':
      if (slug !== load.view.slug) {
        return <Navigate to={`/d/${load.view.id}/${load.view.slug}${location.hash}`} replace />;
      }
      return (
        <div data-testid="player-page" data-deck-id={id} className="contents">
          <Player deck={load.view} />
        </div>
      );
  }
}

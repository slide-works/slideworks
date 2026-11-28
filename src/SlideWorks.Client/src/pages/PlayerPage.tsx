import { useCallback, useRef, useState } from 'react';
import { Navigate, useLocation, useParams } from 'react-router-dom';
import { ApiError, api } from '../api/client';
import type { DeckPlayView, MeView } from '../api/types';
import { AudienceFollower } from '../player/AudienceFollower';
import { Player } from '../player/Player';
import { QaScreenLive } from '../qa/screen/QaScreenLive';
import { ScreenReactionsLive } from '../qa/screen/ScreenReactionsLive';
import { JoinProvider } from '../slides/JoinContext';
import { PollScreenLive } from '../slides/PollScreenLive';
import { OpenResponseScreenLive } from '../openResponses/OpenResponseScreenLive';
import { useDeckRealtime } from '../realtime/useDeckRealtime';
import { useReReadOnJoin } from '../realtime/useReReadOnJoin';
import { PlayerMessage } from './deck/PlayerMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: DeckPlayView }
  // signedIn is null until GET /api/me answers, or when it can't.
  | { kind: 'notFound'; signedIn: boolean | null }
  | { kind: 'failed'; message: string };

// /d/:id/:slug (and /d/:id): the player, open to anonymous viewers, from one
// GET /api/play/{id} (DeckPlayView). A missing or old slug is replaced by the
// deck's own, keeping the query (?present, ?language) and the hash. It re-reads when the deck changes.
export function PlayerPage() {
  const { id = '', slug } = useParams();
  const location = useLocation();
  const search = new URLSearchParams(location.search);
  // ?present is the audience window (P8): it follows the presentation when the viewer owns the deck.
  const audience = search.has('present');
  // ?language= reads the slides in that language (K8). The audience window never sends one, so the big screen stays in the
  // primary language (SW-886 D7). A language the deck doesn't offer comes back in the primary.
  const language = audience ? null : search.get('language') || null;
  const playPath =
    `/api/play/${encodeURIComponent(id)}` +
    (language ? `?language=${encodeURIComponent(language)}` : '');
  // Each read is stamped with its deck, so another deck's result never shows.
  const [result, setResult] = useState<{ id: string; load: Load } | null>(null);
  const load: Load = result?.id === id ? result.load : { kind: 'loading' };

  // Latest wins: every read takes the next number, and only the newest read's
  // answer is shown. A re-read on DeckChanged can overlap another, or outlive
  // a move to another deck, and its older snapshot must never land.
  const latest = useRef(0);

  const read = useCallback(() => {
    const seq = ++latest.current;
    const isLatest = () => seq === latest.current;
    const show = (next: Load) => {
      if (isLatest()) setResult({ id, load: next });
    };
    return api.get<DeckPlayView>(playPath).then(
      (view) => show({ kind: 'loaded', view }),
      (error: unknown) => {
        if (!isLatest()) return;
        if (error instanceof ApiError && error.status === 404) {
          show({ kind: 'notFound', signedIn: null });
          // Only to decide whether to offer sign-in: a private deck reads as not found when signed out.
          api.get<MeView>('/api/me').then(
            () => show({ kind: 'notFound', signedIn: true }),
            (meError: unknown) => {
              if (meError instanceof ApiError && meError.status === 401) {
                show({ kind: 'notFound', signedIn: false });
              }
            },
          );
        } else {
          // A re-read that fails mid-presentation keeps the deck already on screen.
          const message = error instanceof ApiError ? error.message : "Can't reach SlideWorks.";
          setResult((previous) =>
            previous?.id === id && previous.load.kind === 'loaded'
              ? previous
              : { id, load: { kind: 'failed', message } },
          );
        }
      },
    );
  }, [id, playPath]);

  const reReadOnJoin = useReReadOnJoin(read, latest);

  useDeckRealtime(id, () => void read(), reReadOnJoin);

  switch (load.kind) {
    case 'loading':
      return <div data-testid="player-page" data-deck-id={id} className="h-dvh bg-[#05070d]" />;
    case 'notFound': {
      // The window's hash, not the router's: the Player moves it with replaceState,
      // which the router never sees.
      const returnTo = location.pathname + location.search + window.location.hash;
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
        return (
          <Navigate
            to={`/d/${load.view.id}/${load.view.slug}${location.search}${window.location.hash}`}
            replace
          />
        );
      }
      return (
        <div data-testid="player-page" data-deck-id={id} className="contents">
          <JoinProvider deckId={load.view.id} join={load.view.join} owner={load.view.isOwner}>
            <PollScreenLive deckId={load.view.id} owner={load.view.isOwner}>
              <OpenResponseScreenLive deckId={load.view.id} owner={load.view.isOwner}>
                <QaScreenLive deckId={load.view.id} owner={load.view.isOwner}>
                  <ScreenReactionsLive deckId={load.view.id} owner={load.view.isOwner}>
                    {audience ? <AudienceFollower deck={load.view} /> : <Player deck={load.view} />}
                  </ScreenReactionsLive>
                </QaScreenLive>
              </OpenResponseScreenLive>
            </PollScreenLive>
          </JoinProvider>
        </div>
      );
  }
}

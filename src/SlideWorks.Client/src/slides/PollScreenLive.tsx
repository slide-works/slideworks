import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../guest/audienceEvents';
import { useLiveSessionContext } from '../live/LiveSessionProvider';
import { readPollControl } from '../polls/presenter/pollControlApi';
import type { PollControlView } from '../polls/types';
import type { PresentationState } from '../presentation/types';
import { useDeckConnection } from '../realtime/useDeckConnection';
import { useJoin } from './JoinContext';
import { PollScreenProvider, pollScreenOf } from './PollContext';
import { useSlideNumber } from './slideNumber';

export type PollScreenLiveProps = {
  deckId: string;
  // The same flag JoinProvider takes: the viewer may be a person who can present the deck (DeckPlayView.isOwner, which
  // the server computes with the can-present rule). Anyone else gets null, and the provider makes no call.
  owner: boolean;
  children: ReactNode;
};

// SW-4's presentation state broadcast. The page's own presentation join (the audience window, or the live player's
// publisher) puts the shared connection in the deck's presentation group; this provider only listens to it.
const PRESENTATION_STATE_METHOD = 'presentationState';

// Tells the provider which slides under it are poll slides, so it reads a poll only for a slide that has one.
type MarkPollSlide = (slide: number) => () => void;
const PollSlidesContext = createContext<MarkPollSlide | null>(null);

// Called by the poll layout: marks the slide it is rendered on as a poll slide for the nearest PollScreenLive, for as
// long as it is mounted. Does nothing outside one (the deck page, RenderPage, preview_deck) or outside a slide.
export function useMarkPollSlide(): void {
  const mark = useContext(PollSlidesContext);
  const slide = useSlideNumber();
  useEffect(() => {
    if (mark === null || slide === null) return;
    return mark(slide);
  }, [mark, slide]);
}

// A state is newer when its epoch differs from the last one, or its seq is higher (SW-4 P1).
function newerState(next: PresentationState, last: PresentationState | null): boolean {
  return last === null || next.epoch !== last.epoch || next.seq > last.seq;
}

function isPresentationState(value: unknown): value is PresentationState {
  if (typeof value !== 'object' || value === null) return false;
  const state = value as Partial<PresentationState>;
  return (
    typeof state.deckId === 'string' &&
    typeof state.epoch === 'string' &&
    typeof state.seq === 'number' &&
    typeof state.slide === 'number'
  );
}

// The poll slide's live screen in the player and the audience window (SW-6 V7), inside JoinProvider. For a person who
// can present the deck (the owner flag, which the play view computes from the same can-present rule as GET /live and the
// poll routes), it follows the presenter's slide from SW-4's presentation state from the moment it mounts, so a state
// that arrives before the live session is confirmed, or before the deck goes live, is not lost. While the live session
// confirms the viewer and the deck is live, it watches the deck's audience (WatchAudience, again after every
// reconnect), reads GET /api/decks/{id}/polls/{slide} when the presenter's slide is a poll slide (on arrival, after
// each watch, and when a poll slide appears there), and follows pollControl. Views are kept per slide and a view with a
// lower or equal seq than the one held for its slide is dropped, as is a read for a slide the presenter has left. The
// screen is the presenter's slide's view through pollScreenOf, so counts stay off the slide while the room can't see
// them. Anyone else, a page outside a JoinProvider, a deck that isn't live and a page with no presentation state yet
// get null: empty tracks.
export function PollScreenLive({ deckId, owner, children }: PollScreenLiveProps) {
  // Outside a JoinProvider there is no live session to confirm the viewer, so nothing to follow.
  const inJoin = useJoin() !== null;
  const listening = owner && deckId !== '' && inJoin;
  // The flag only says the viewer may present; the deck's live session (JoinProvider's) confirms it, as it does for
  // the live chip. Rounds exist only in a live session, so there is no live data while not live.
  const session = useLiveSessionContext();
  const active = listening && session.liveView !== null && session.live;
  const connection = useDeckConnection();
  const [presenterSlide, setPresenterSlide] = useState<number | null>(null);
  const [views, setViews] = useState<ReadonlyMap<number, PollControlView>>(new Map());
  // How many times WatchAudience has succeeded on this connection: each success (the first, and after a reconnect)
  // reads the presenter's poll again, since pollControl events sent while offline are lost.
  const [watched, setWatched] = useState(0);
  // Poll slides under this provider, counted, since a slide may be rendered more than once.
  const [pollSlides, setPollSlides] = useState<ReadonlyMap<number, number>>(new Map());

  const apply = useCallback((view: PollControlView) => {
    setViews((held) => {
      const current = held.get(view.slide);
      if (current !== undefined && view.seq <= current.seq) return held;
      return new Map(held).set(view.slide, view);
    });
  }, []);

  // A new deck starts from nothing. The presenter's slide outlives a live session ending or starting (the presentation
  // goes on and sends nothing new), but the views and the watch don't: no view of an earlier session shows, and no
  // read comes before the next watch.
  const [shownDeck, setShownDeck] = useState(deckId);
  if (shownDeck !== deckId) {
    setShownDeck(deckId);
    setPresenterSlide(null);
  }
  const scope = `${deckId}:${active}`;
  const [shownScope, setShownScope] = useState(scope);
  if (shownScope !== scope) {
    setShownScope(scope);
    setViews(new Map());
    setWatched(0);
  }

  const lastState = useRef<PresentationState | null>(null);
  useEffect(() => {
    if (!listening) return;
    lastState.current = null;
    return connection.on(PRESENTATION_STATE_METHOD, (state: unknown) => {
      if (!isPresentationState(state) || state.deckId !== deckId) return;
      if (!newerState(state, lastState.current)) return;
      lastState.current = state;
      setPresenterSlide(state.slide);
    });
  }, [listening, connection, deckId]);

  useEffect(() => {
    if (!active) return;
    const stopEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (isAudienceEvent(event) && event.type === 'pollControl') apply(event.control);
    });
    let gone = false;
    // The server forgets a dead connection's groups, so watch again each time the connection is up. A refusal means
    // this viewer can't present the deck: no poll is read and the slide keeps its empty tracks.
    const stopUp = connection.onUp(() => {
      connection.invoke('WatchAudience', deckId).then(
        () => {
          if (!gone) setWatched((count) => count + 1);
        },
        () => undefined,
      );
    });
    return () => {
      gone = true;
      stopEvents();
      stopUp();
    };
  }, [active, connection, deckId, apply]);

  const isPollSlide = presenterSlide !== null && pollSlides.has(presenterSlide);
  useEffect(() => {
    if (!active || watched === 0 || presenterSlide === null || !isPollSlide) return;
    let gone = false;
    // A failed read leaves the slide as it was; the next arrival, reconnect or pollControl brings it up to date. The
    // big screen shows the room no error.
    readPollControl(deckId, presenterSlide).then(
      (view) => {
        if (!gone) apply(view);
      },
      () => undefined,
    );
    return () => {
      gone = true;
    };
  }, [active, deckId, presenterSlide, isPollSlide, watched, apply]);

  const mark = useCallback<MarkPollSlide>((slide) => {
    setPollSlides((held) => new Map(held).set(slide, (held.get(slide) ?? 0) + 1));
    return () =>
      setPollSlides((held) => {
        const next = new Map(held);
        const count = (next.get(slide) ?? 0) - 1;
        if (count > 0) next.set(slide, count);
        else next.delete(slide);
        return next;
      });
  }, []);

  const view = active && presenterSlide !== null ? views.get(presenterSlide) : undefined;
  const source = useMemo(() => (view === undefined ? null : pollScreenOf(view)), [view]);

  return (
    <PollSlidesContext value={listening ? mark : null}>
      <PollScreenProvider source={source}>{children}</PollScreenProvider>
    </PollSlidesContext>
  );
}

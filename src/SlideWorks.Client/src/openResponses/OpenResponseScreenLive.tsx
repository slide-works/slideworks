import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api } from '../api/client';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../guest/audienceEvents';
import { useLiveSessionContext } from '../live/LiveSessionProvider';
import { useDeckConnection } from '../realtime/useDeckConnection';
import { useJoin } from '../slides/JoinContext';
import { useSlideNumber } from '../slides/slideNumber';
import type { OpenResponseControlView } from './types';
import { newerRound, type RoomEvent } from './useOpenResponse';

export type OpenResponseScreenLiveProps = {
  deckId: string;
  // The same flag PollScreenLive takes: the viewer may present the deck (DeckPlayView.isOwner).
  owner: boolean;
  children: ReactNode;
};

type Screen = {
  mark: (slide: number) => () => void;
  screens: ReadonlyMap<number, RoomEvent>;
};

const ScreenContext = createContext<Screen | null>(null);

// Called by the word-cloud and open-text layouts: the newest round of the slide the layout is rendered on, with hidden
// entries left out, while the nearest OpenResponseScreenLive follows it. Null outside one, outside a slide, and until the
// first read or event for the slide arrives.
export function useOpenResponseScreen(): RoomEvent | null {
  const screen = useContext(ScreenContext);
  const slide = useSlideNumber();
  const mark = screen?.mark;
  useEffect(() => {
    if (mark === undefined || slide === null) return;
    return mark(slide);
  }, [mark, slide]);
  return screen !== null && slide !== null ? (screen.screens.get(slide) ?? null) : null;
}

// The big screen never shows a hidden entry (SW-8 D4, D6). A round's entries show as soon as they arrive (D14).
function shownOf(event: RoomEvent): RoomEvent {
  return event.type === 'wordCloud'
    ? { ...event, words: event.words.filter((word) => !word.hidden) }
    : { ...event, answers: event.answers.filter((answer) => !answer.hidden) };
}

function screenOfControl(slide: number, view: OpenResponseControlView): RoomEvent {
  const round =
    view.phase === 'draft' ? { phase: view.phase } : { phase: view.phase, round: view.round };
  const fields = {
    ...round,
    slide,
    number: view.number,
    revision: view.revision,
    revealed: view.revealed,
    guests: view.guests,
  };
  const shown = view.entries.filter((entry) => !entry.hidden);
  return view.kind === 'word-cloud'
    ? {
        ...fields,
        type: 'wordCloud',
        words: shown.map((entry) => ({ text: entry.text, count: entry.count!, hidden: false })),
      }
    : {
        ...fields,
        type: 'openText',
        answers: shown.map((entry) => ({ number: entry.number, text: entry.text, hidden: false })),
      };
}

// The word-cloud and open-text slides' live screen in the player and the audience window (SW-8 D6), inside JoinProvider.
// For a person who can present the deck, while the live session confirms them and the deck is live, it watches the deck's
// audience (WatchAudience, again after every reconnect), reads GET /api/decks/{id}/slides/{slide}/response for each slide
// a layout marked after each watch and when a slide is marked, and follows wordCloud and openText. Each slide keeps its
// newest round. Anyone else, and a page outside a JoinProvider, get null.
export function OpenResponseScreenLive({ deckId, owner, children }: OpenResponseScreenLiveProps) {
  const inJoin = useJoin() !== null;
  const listening = owner && deckId !== '' && inJoin;
  const session = useLiveSessionContext();
  const active = listening && session.liveView !== null && session.live;
  const connection = useDeckConnection();
  const [screens, setScreens] = useState<ReadonlyMap<number, RoomEvent>>(new Map());
  // How many times WatchAudience has succeeded on this connection: each success reads the marked slides again, since
  // events sent while offline are lost.
  const [watched, setWatched] = useState(0);
  const [marked, setMarked] = useState<ReadonlyMap<number, number>>(new Map());

  const apply = useCallback((event: RoomEvent) => {
    const shown = shownOf(event);
    setScreens((held) =>
      held.has(shown.slide) && !newerRound(shown, held.get(shown.slide)!)
        ? held
        : new Map(held).set(shown.slide, shown),
    );
  }, []);

  // A new deck or a live session starting or ending starts from nothing, and no read comes before the next watch.
  const scope = `${deckId}:${active}`;
  const [shownScope, setShownScope] = useState(scope);
  if (shownScope !== scope) {
    setShownScope(scope);
    setScreens(new Map());
    setWatched(0);
  }

  useEffect(() => {
    if (!active) return;
    const stopEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (isAudienceEvent(event) && (event.type === 'wordCloud' || event.type === 'openText'))
        apply(event);
    });
    let gone = false;
    // A refusal means this viewer can't present the deck: nothing is read and the slide shows no entries.
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

  // The marked slides as a stable key, so marking a slide again does not read every slide again.
  const slides = [...marked.keys()].sort((x, y) => x - y).join(',');
  useEffect(() => {
    if (!active || watched === 0) return;
    let gone = false;
    // A failed read leaves the slide as it was; the next watch or event brings it up to date. The big screen shows the
    // room no error.
    for (const slide of slides === '' ? [] : slides.split(',').map(Number)) {
      api
        .get<OpenResponseControlView>(
          `/api/decks/${encodeURIComponent(deckId)}/slides/${slide}/response`,
        )
        .then(
          (view) => {
            if (!gone) apply(screenOfControl(slide, view));
          },
          () => undefined,
        );
    }
    return () => {
      gone = true;
    };
  }, [active, deckId, slides, watched, apply]);

  const mark = useCallback((slide: number) => {
    setMarked((held) => new Map(held).set(slide, (held.get(slide) ?? 0) + 1));
    return () =>
      setMarked((held) => {
        const next = new Map(held);
        const count = (next.get(slide) ?? 0) - 1;
        if (count > 0) next.set(slide, count);
        else next.delete(slide);
        return next;
      });
  }, []);

  const value = useMemo<Screen | null>(
    () => (listening ? { mark, screens: active ? screens : new Map() } : null),
    [listening, mark, active, screens],
  );

  return <ScreenContext value={value}>{children}</ScreenContext>;
}

const NO_MARK = () => () => undefined;

// A fixed screen for each slide, for a page that draws results it was handed and follows nothing (the print page). Each
// event is given to the layout of its slide as it is; marking a slide does nothing.
export function OpenResponseScreenStatic({
  events,
  children,
}: {
  events: readonly RoomEvent[];
  children: ReactNode;
}) {
  const value = useMemo<Screen>(
    () => ({ mark: NO_MARK, screens: new Map(events.map((event) => [event.slide, event])) }),
    [events],
  );
  return <ScreenContext value={value}>{children}</ScreenContext>;
}

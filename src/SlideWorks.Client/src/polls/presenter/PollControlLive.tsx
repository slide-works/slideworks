import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import type { PollControlView } from '../types';
import { readPollControl, sendPollAction } from './pollControlApi';
import { pollActionFailed, POLL_READ_FAILED } from './pollMessages';
import { PollControlContext, type PollAction, type PollControlSource } from './usePollControl';

export type PollControlLiveProps = {
  deckId: string;
  // The presenter's slide, 1-based, from SW-4's presentation state.
  slide: number;
  children: ReactNode;
};

// SW-1's deck events on the shared connection. The page that holds this provider has joined the deck's group (the
// presenter window through useDeckRealtime); this provider only listens, so it adds no join of its own.
const DECK_EVENT_METHOD = 'deckEvent';

// What is known about each slide: its poll's latest view, or null when the poll route said it has no poll (404).
type Polls = ReadonlyMap<number, PollControlView | null>;

// A message for the slide it is about, so moving on leaves it behind.
type SlideError = { slide: number; message: string };

const isDeckEventFor = (deckId: string, event: unknown) =>
  typeof event === 'object' && event !== null && (event as { deckId?: unknown }).deckId === deckId;

// The presenter's poll controls in the presenter window and on the phone remote (SW-6 V8). From the moment it mounts it
// listens for pollControl on the page's shared hub connection; each time the connection is up (first, and after every
// reconnect) it watches the deck's audience (WatchAudience, which also carries pollControl) and then reads
// GET /api/decks/{id}/polls/{slide}. It reads again when the presenter's slide changes, when the deck changes (a deck
// event: slide numbers resolve at call time) and after a refused action. Views are kept per slide and a view whose seq
// is not higher than the one held for its slide is dropped, as is a read for a slide the presenter has left. A 404 means
// the slide has no poll: control is null. The five actions POST their route once each and never reject: a refusal (409)
// or a failure sets error to a sentence the presenter can act on, never the raw code.
export function PollControlLive({ deckId, slide, children }: PollControlLiveProps) {
  const connection = useDeckConnection();
  const [polls, setPolls] = useState<Polls>(new Map());
  const [error, setError] = useState<SlideError | null>(null);
  // How many times WatchAudience has succeeded on this connection, plus one per deck event: each is a reason to read
  // the presenter's poll again, since events sent before a watch, or while offline, are lost.
  const [reads, setReads] = useState(0);
  const watched = useRef(false);

  // A new deck starts from nothing: no view or message of another deck shows, and no read comes before its watch.
  const [shownDeck, setShownDeck] = useState(deckId);
  if (shownDeck !== deckId) {
    setShownDeck(deckId);
    setPolls(new Map());
    setError(null);
    setReads(0);
  }
  // A message is about the slide it came from: moving on leaves it behind, and coming back doesn't bring it back.
  const [shownSlide, setShownSlide] = useState(slide);
  if (shownSlide !== slide) {
    setShownSlide(slide);
    setError(null);
  }

  const apply = useCallback((view: PollControlView) => {
    setPolls((held) => {
      const current = held.get(view.slide);
      if (current != null && view.seq <= current.seq) return held;
      return new Map(held).set(view.slide, view);
    });
  }, []);

  const markNoPoll = useCallback((at: number) => {
    setPolls((held) => (held.get(at) === null ? held : new Map(held).set(at, null)));
  }, []);

  useEffect(() => {
    watched.current = false;
    const stopPollEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (isAudienceEvent(event) && event.type === 'pollControl') apply(event.control);
    });
    const stopDeckEvents = connection.on(DECK_EVENT_METHOD, (event: unknown) => {
      if (isDeckEventFor(deckId, event) && watched.current) setReads((count) => count + 1);
    });
    let gone = false;
    // The server forgets a dead connection's groups, so watch again each time the connection is up, then read. A refused
    // watch means this person can't present the deck, which the page itself already shows; a dropped call is retried
    // when the connection is up again. Either way no poll is read, and the panel shows nothing.
    const stopUp = connection.onUp(() => {
      connection.invoke('WatchAudience', deckId).then(
        () => {
          if (gone) return;
          watched.current = true;
          setReads((count) => count + 1);
        },
        () => undefined,
      );
    });
    return () => {
      gone = true;
      stopPollEvents();
      stopDeckEvents();
      stopUp();
    };
  }, [connection, deckId, apply]);

  const read = useCallback(
    (at: number, isGone: () => boolean) =>
      readPollControl(deckId, at).then(
        (view) => {
          if (isGone()) return;
          apply(view);
          // A read that works ends an earlier failed read's message; a refusal's message stays.
          setError((shown) => (shown?.message === POLL_READ_FAILED ? null : shown));
        },
        (failure: unknown) => {
          if (isGone()) return;
          if (failure instanceof ApiError && failure.status === 404) markNoPoll(at);
          else setError({ slide: at, message: POLL_READ_FAILED });
        },
      ),
    [deckId, apply, markNoPoll],
  );

  useEffect(() => {
    if (reads === 0) return;
    let gone = false;
    void read(slide, () => gone);
    return () => {
      gone = true;
    };
  }, [reads, slide, read]);

  // The slide each action is for, as of the last render: an action always runs the poll on screen.
  const latestSlide = useRef(slide);
  useEffect(() => {
    latestSlide.current = slide;
  }, [slide]);

  const act = useCallback(
    (action: PollAction) => async () => {
      const at = latestSlide.current;
      setError(null);
      try {
        apply(await sendPollAction(deckId, at, action));
      } catch (failure) {
        setError({ slide: at, message: pollActionFailed(action, failure) });
        // After a refusal (or a poll that is gone) the poll is not where this window thought it was: read it so the
        // buttons match its phase.
        if (failure instanceof ApiError && (failure.status === 409 || failure.status === 404))
          await read(at, () => latestSlide.current !== at);
      }
    },
    [deckId, apply, read],
  );

  const control = polls.get(slide) ?? null;
  const message = error?.slide === slide ? error.message : null;
  const source = useMemo<PollControlSource>(
    () => ({
      deckId,
      value: {
        control,
        open: act('open'),
        close: act('close'),
        reveal: act('reveal'),
        showAnswer: act('showAnswer'),
        newRound: act('newRound'),
        error: message,
      },
    }),
    [deckId, control, message, act],
  );
  return <PollControlContext value={source}>{children}</PollControlContext>;
}

import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import type { PollControlView } from '../types';
import { readPollControl, sendPollAction } from './pollControlApi';
import { pollActionFailed, POLL_READ_FAILED, POLL_WATCH_FAILED } from './pollMessages';
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

// After a WatchAudience that failed while the connection stayed up: wait this long before each retry, then stop until
// the slide changes or the connection comes back. A timer, never a clock.
const WATCH_RETRY_DELAYS_MS: readonly number[] = [2000, 5000, 15000];

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
// event: slide numbers resolve at call time) and after any failed action. Views are kept per slide and a view whose seq
// is not higher than the one held for its slide is dropped. A read's answer, a 404 included, is dropped when the
// presenter has left its slide or when a pollControl or an action's answer for that slide arrived after the read was
// sent, since a 404 has no seq to order it by. A 404 means the slide has no poll: control is null.
//
// A watch that fails is retried on a short backoff and on every slide change. The poll is still read, so the panel
// shows it, with a message that live updates aren't arriving. The five actions POST their route once each and never
// reject: a refusal (409) or a failure sets error to a sentence the presenter can act on, never the raw code.
export function PollControlLive({ deckId, slide, children }: PollControlLiveProps) {
  const connection = useDeckConnection();
  const [polls, setPolls] = useState<Polls>(new Map());
  const [error, setError] = useState<SlideError | null>(null);
  // Each finished watch attempt, plus one per deck event: each is a reason to read the presenter's poll again, since
  // events sent before a watch, or while offline, are lost.
  const [reads, setReads] = useState(0);
  // The last watch attempt failed: the panel says live updates aren't arriving.
  const [watchFailed, setWatchFailed] = useState(false);
  // Whether a watch has been tried on this connection, so deck events can read; and a retry of the watch, for a slide
  // change while it has failed.
  const watchTried = useRef(false);
  const failedWatch = useRef(false);
  const retryWatch = useRef<() => void>(() => undefined);
  // When a view for each slide last arrived from an event or an action, as a count: a read sent before it is older.
  const tick = useRef(0);
  const arrivals = useRef(new Map<number, number>());

  // A new deck starts from nothing: no view or message of another deck shows, and no read comes before its watch.
  const [shownDeck, setShownDeck] = useState(deckId);
  if (shownDeck !== deckId) {
    setShownDeck(deckId);
    setPolls(new Map());
    setError(null);
    setReads(0);
    setWatchFailed(false);
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

  // A view from an event or an action: newer than any read for its slide that is still on its way.
  const arrive = useCallback(
    (view: PollControlView) => {
      arrivals.current.set(view.slide, ++tick.current);
      apply(view);
    },
    [apply],
  );

  const markNoPoll = useCallback((at: number) => {
    setPolls((held) => (held.get(at) === null ? held : new Map(held).set(at, null)));
  }, []);

  useEffect(() => {
    watchTried.current = false;
    failedWatch.current = false;
    arrivals.current = new Map();
    const stopPollEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (isAudienceEvent(event) && event.type === 'pollControl') arrive(event.control);
    });
    const stopDeckEvents = connection.on(DECK_EVENT_METHOD, (event: unknown) => {
      if (isDeckEventFor(deckId, event) && watchTried.current) setReads((count) => count + 1);
    });
    let gone = false;
    let retries = 0;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    // The server forgets a dead connection's groups, so watch again each time the connection is up, then read. The read
    // follows a failed watch too: the panel shows the poll and says live updates aren't arriving, and the watch is tried
    // again after a short wait, a few times, and on every slide change.
    const watch = () => {
      clearTimeout(retryTimer);
      connection.invoke('WatchAudience', deckId).then(
        () => {
          if (gone) return;
          retries = 0;
          failedWatch.current = false;
          setWatchFailed(false);
          watchTried.current = true;
          setReads((count) => count + 1);
        },
        () => {
          if (gone) return;
          failedWatch.current = true;
          setWatchFailed(true);
          watchTried.current = true;
          setReads((count) => count + 1);
          if (retries < WATCH_RETRY_DELAYS_MS.length)
            retryTimer = setTimeout(watch, WATCH_RETRY_DELAYS_MS[retries++]);
        },
      );
    };
    retryWatch.current = () => {
      if (failedWatch.current) watch();
    };
    const stopUp = connection.onUp(() => {
      retries = 0;
      watch();
    });
    return () => {
      gone = true;
      clearTimeout(retryTimer);
      retryWatch.current = () => undefined;
      stopPollEvents();
      stopDeckEvents();
      stopUp();
    };
  }, [connection, deckId, arrive]);

  // A new slide is a new chance for a watch that failed.
  const watchedSlide = useRef(slide);
  useEffect(() => {
    if (watchedSlide.current === slide) return;
    watchedSlide.current = slide;
    retryWatch.current();
  }, [slide]);

  const read = useCallback(
    (at: number, isGone: () => boolean) => {
      const sentAt = tick.current;
      const stale = () => isGone() || (arrivals.current.get(at) ?? 0) > sentAt;
      return readPollControl(deckId, at).then(
        (view) => {
          if (stale()) return;
          apply(view);
          // A read that works ends an earlier failed read's message; a refusal's message stays.
          setError((shown) => (shown?.message === POLL_READ_FAILED ? null : shown));
        },
        (failure: unknown) => {
          if (stale()) return;
          if (failure instanceof ApiError && failure.status === 404) markNoPoll(at);
          // A failed read never hides a message still showing for this slide, such as a refusal.
          else
            setError((shown) =>
              shown?.slide === at && shown.message !== POLL_READ_FAILED
                ? shown
                : { slide: at, message: POLL_READ_FAILED },
            );
        },
      );
    },
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
        arrive(await sendPollAction(deckId, at, action));
      } catch (failure) {
        setError({ slide: at, message: pollActionFailed(action, failure) });
        // After a refusal, a poll that is gone or a call that may have landed before it failed, the poll may not be
        // where this window thought it was: read it so the buttons match its phase.
        await read(at, () => latestSlide.current !== at);
      }
    },
    [deckId, arrive, read],
  );

  const control = polls.get(slide) ?? null;
  const message =
    error?.slide === slide
      ? error.message
      : watchFailed && control !== null
        ? POLL_WATCH_FAILED
        : null;
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

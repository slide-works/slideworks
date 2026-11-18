import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import { REACTION_EMOJI } from '../emoji';
import {
  QA_REFUSALS,
  type QaAction,
  type QaControlView,
  type QaRefusalCode,
  type ReactionCounts,
} from '../types';
import { readQaControl, sendQaAction, updateQaSettings, updateQaSlide } from './qaControlApi';
import { QaControlContext, type QaControlSource } from './useQaControl';

export type QaControlLiveProps = {
  deckId: string;
  // The presenter's slide, 1-based, from SW-4's presentation state; null on the deck page, which has no presenter.
  slide: number | null;
  children: ReactNode;
};

// SW-1's deck events on the shared connection (a deck edit, the deck going live or ending). The page that holds this
// provider has joined the deck's group through useDeckRealtime; this provider only listens.
const DECK_EVENT_METHOD = 'deckEvent';

// After a WatchAudience that failed while the connection stayed up: wait this long before each retry, then stop until
// the slide changes or the connection comes back. A timer, never a clock.
const WATCH_RETRY_DELAYS_MS: readonly number[] = [2000, 5000, 15000];

const READ_FAILED =
  "Couldn't load the questions. Check your connection; they load again when you move or reconnect.";

const WATCH_FAILED =
  "Live question updates aren't reaching this window, so this list may be out of date. It keeps trying, and tries again when you change slides.";

const REFUSALS: Readonly<Record<QaRefusalCode, string>> = {
  not_live: "The deck isn't live, so its questions can't be moderated. Go live first.",
  wrong_state: 'That question had already changed. The buttons now show what you can do.',
};

// What each call was trying to do, for its message.
type Call = QaAction | 'settings' | 'slide';

const CALL_NAMES: Readonly<Record<Call, string>> = {
  approve: 'approve the question',
  hide: 'hide the question',
  restore: 'restore the question',
  answer: 'mark the question answered',
  reopen: 'reopen the question',
  show: 'show the question',
  unshow: 'take the question off screen',
  settings: 'change the question settings',
  slide: "change this slide's switch",
};

const isRefusal = (code: string): code is QaRefusalCode =>
  (QA_REFUSALS as readonly string[]).includes(code);

// The message for a failed call: a refusal's sentence for a 409 with its code, else what went wrong and what to do.
// Never the raw code.
function failedMessage(call: Call, failure: unknown): string {
  const what = `Couldn't ${CALL_NAMES[call]}.`;
  if (!(failure instanceof ApiError)) return `${what} Check your connection and try again.`;
  if (failure.status === 409 && isRefusal(failure.message)) return REFUSALS[failure.message];
  if (failure.status === 409) return `${what} The questions had changed. The list now shows them.`;
  if (failure.status === 404) return `${what} It isn't there any more. The list now shows what is.`;
  if (failure.status === 403)
    return `${what} Only people who can present this deck can moderate its questions.`;
  return `${what} Try again in a moment.`;
}

const NO_REACTIONS: ReactionCounts = { thumbsUp: 0, heart: 0, laugh: 0, wow: 0, clap: 0 };

const plus = (a: ReactionCounts, b: ReactionCounts): ReactionCounts =>
  Object.fromEntries(REACTION_EMOJI.map((key) => [key, a[key] + b[key]])) as ReactionCounts;

// The latest view, and the reaction batches received since it: a newer view replaces both, since its totals already
// hold every batch before it.
type Held = { view: QaControlView | null; bursts: ReactionCounts };

const isDeckEventFor = (deckId: string, event: unknown) =>
  typeof event === 'object' && event !== null && (event as { deckId?: unknown }).deckId === deckId;

// The presenter's Q&A controls in the presenter window, on the phone remote and on the deck page (SW-9 Q9), for a
// person who can present the deck. From the moment it mounts it listens on the page's shared hub connection for
// questionControl and reactions; each time the connection is up (first, and after every reconnect) it watches the
// deck's audience (WatchAudience) and then reads GET /api/decks/{id}/qa. It reads again when `slide` changes (nothing
// publishes questionControl when the presenter moves, and the view's `here` is the presenter's slide), on a deck event
// for the deck (it went live or ended) and after any failed call. A view whose seq is not higher than the one held is
// dropped, whether it came from a read, an event or an action.
//
// `control.reactions` is the held view's totals plus the reactions batches received since it, so the counter moves
// between views; a newer view replaces both.
//
// A watch that fails is retried on a short backoff and on every slide change; the questions are still read, with a
// message that live updates aren't arriving. Each action and settings change sends its route once, takes the view it
// answers and never rejects: a refusal or failure sets error to a sentence, never the raw code, and is also the value
// the call settles with (null on success).
export function QaControlLive({ deckId, slide, children }: QaControlLiveProps) {
  const connection = useDeckConnection();
  const [held, setHeld] = useState<Held>({ view: null, bursts: NO_REACTIONS });
  const [error, setError] = useState<string | null>(null);
  // Each finished watch attempt, plus one per deck event: each is a reason to read again, since events sent before a
  // watch, or while offline, are lost.
  const [reads, setReads] = useState(0);
  const [watchFailed, setWatchFailed] = useState(false);
  const watchTried = useRef(false);
  const failedWatch = useRef(false);
  const retryWatch = useRef<() => void>(() => undefined);

  // A new deck starts from nothing: no view or message of another deck shows, and no read comes before its watch.
  const [shownDeck, setShownDeck] = useState(deckId);
  if (shownDeck !== deckId) {
    setShownDeck(deckId);
    setHeld({ view: null, bursts: NO_REACTIONS });
    setError(null);
    setReads(0);
    setWatchFailed(false);
  }

  const apply = useCallback((view: QaControlView) => {
    setHeld((current) =>
      current.view !== null && view.seq <= current.view.seq
        ? current
        : { view, bursts: NO_REACTIONS },
    );
  }, []);

  useEffect(() => {
    watchTried.current = false;
    failedWatch.current = false;
    const stopAudienceEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (!isAudienceEvent(event)) return;
      if (event.type === 'questionControl') apply(event.control);
      else if (event.type === 'reactions')
        setHeld((current) => ({ ...current, bursts: plus(current.bursts, event.counts) }));
    });
    const stopDeckEvents = connection.on(DECK_EVENT_METHOD, (event: unknown) => {
      if (isDeckEventFor(deckId, event) && watchTried.current) setReads((count) => count + 1);
    });
    let gone = false;
    let retries = 0;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    // The server forgets a dead connection's groups, so watch again each time the connection is up, then read. The read
    // follows a failed watch too, and the watch is tried again after a short wait, a few times, and on a slide change.
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
      stopAudienceEvents();
      stopDeckEvents();
      stopUp();
    };
  }, [connection, deckId, apply]);

  // A new slide is a new chance for a watch that failed.
  const watchedSlide = useRef(slide);
  useEffect(() => {
    if (watchedSlide.current === slide) return;
    watchedSlide.current = slide;
    retryWatch.current();
  }, [slide]);

  const read = useCallback(
    (isGone: () => boolean) =>
      readQaControl(deckId).then(
        (view) => {
          if (isGone()) return;
          apply(view);
          // A read that works ends an earlier failed read's message; any other message stays until the next call.
          setError((shown) => (shown === READ_FAILED ? null : shown));
        },
        () => {
          // A failed read never hides a message still showing, such as a refusal.
          if (!isGone()) setError((shown) => shown ?? READ_FAILED);
        },
      ),
    [deckId, apply],
  );

  useEffect(() => {
    if (reads === 0) return;
    let gone = false;
    void read(() => gone);
    return () => {
      gone = true;
    };
  }, [reads, slide, read]);

  const latestDeck = useRef(deckId);
  useEffect(() => {
    latestDeck.current = deckId;
  }, [deckId]);

  const send = useCallback(
    async (call: Call, request: () => Promise<QaControlView>): Promise<string | null> => {
      setError(null);
      try {
        const view = await request();
        // An answer for a deck this window has moved on from is not the new deck's view.
        if (latestDeck.current === deckId) apply(view);
        return null;
      } catch (failure) {
        const message = failedMessage(call, failure);
        if (latestDeck.current !== deckId) return message;
        setError(message);
        // After a refusal or a failure the questions may not be where this window thought they were: read them so the
        // buttons match.
        await read(() => latestDeck.current !== deckId);
        return message;
      }
    },
    [deckId, apply, read],
  );

  const control = useMemo(
    () =>
      held.view === null
        ? null
        : { ...held.view, reactions: plus(held.view.reactions, held.bursts) },
    [held],
  );
  const message = error ?? (watchFailed && control !== null ? WATCH_FAILED : null);
  const source = useMemo<QaControlSource>(() => {
    const act = (action: QaAction) => (number: number) =>
      send(action, () => sendQaAction(deckId, action, number));
    return {
      deckId,
      slide,
      value: {
        control,
        approve: act('approve'),
        hide: act('hide'),
        restore: act('restore'),
        answer: act('answer'),
        reopen: act('reopen'),
        show: act('show'),
        unshow: act('unshow'),
        setSettings: (patch) => send('settings', () => updateQaSettings(deckId, patch)),
        setSlide: (at, patch) => send('slide', () => updateQaSlide(deckId, at, patch)),
        error: message,
      },
    };
  }, [deckId, slide, control, message, send]);
  return <QaControlContext value={source}>{children}</QaControlContext>;
}

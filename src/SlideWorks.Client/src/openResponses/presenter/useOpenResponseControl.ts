import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { ApiError, api } from '../../api/client';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import { openResponseErrorSentence } from '../errors';
import type { OpenResponseControlView } from '../types';

// What the presenter panel and the phone strip read. There is no reveal: results stay live (D14).
export type OpenResponseControlValue = {
  control: OpenResponseControlView | null;
  open(): Promise<void>;
  close(): Promise<void>;
  newRound(): Promise<void>;
  hide(number: number): Promise<void>;
  restore(number: number): Promise<void>;
  error: string | null;
};

type SlideError = { slide: number; message: string; from: 'read' | 'action' };

type Views = ReadonlyMap<number, OpenResponseControlView | null>;

type Source = { deckId: string; value: OpenResponseControlValue };

export const OpenResponseControlContext = createContext<Source | null>(null);

// A slide that has not been read yet is missing. null means the route said this slide is not a word cloud or open text.
const WATCH_RETRY_DELAYS_MS: readonly number[] = [2000, 5000, 15000];

const WATCH_FAILED = "Live updates aren't arriving. The entries shown may be behind.";

function responsePath(deckId: string, slide: number): string {
  return `/api/decks/${encodeURIComponent(deckId)}/slides/${slide}/response`;
}

function sentenceFor(failure: unknown): string {
  return openResponseErrorSentence(failure instanceof ApiError ? failure.code : undefined);
}

// The presenter's open-response controls for one deck. From the moment it is used it watches the deck's audience
// (WatchAudience, again after every reconnect) and reads GET /api/decks/{id}/slides/{slide}/response. It is not
// useOpenResponse. A 404 means the slide is not a word cloud or open text. Any other failed read keeps the view already
// held and shows the sentence for the error code. Open, close, new round, hide and restore POST their routes and never
// reject.
export function useOpenResponseControl(deckId: string, slide: number): OpenResponseControlValue {
  const connection = useDeckConnection();
  const [views, setViews] = useState<Views>(new Map());
  const [error, setError] = useState<SlideError | null>(null);
  const [reads, setReads] = useState(0);
  const [watchFailed, setWatchFailed] = useState(false);
  const watchTried = useRef(false);
  const failedWatch = useRef(false);
  const retryWatch = useRef<() => void>(() => undefined);
  const tick = useRef(0);
  const arrivals = useRef(new Map<number, number>());
  const latestSlide = useRef(slide);
  useEffect(() => {
    latestSlide.current = slide;
  }, [slide]);

  const [shownDeck, setShownDeck] = useState(deckId);
  if (shownDeck !== deckId) {
    setShownDeck(deckId);
    setViews(new Map());
    setError(null);
    setReads(0);
    setWatchFailed(false);
  }
  const [shownSlide, setShownSlide] = useState(slide);
  if (shownSlide !== slide) {
    setShownSlide(slide);
    setError(null);
  }

  const arrive = useCallback((at: number, view: OpenResponseControlView) => {
    arrivals.current.set(at, ++tick.current);
    setViews((held) => new Map(held).set(at, view));
  }, []);

  const markAbsent = useCallback((at: number) => {
    setViews((held) => (held.get(at) === null ? held : new Map(held).set(at, null)));
  }, []);

  useEffect(() => {
    watchTried.current = false;
    failedWatch.current = false;
    arrivals.current = new Map();
    const stopEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (!watchTried.current || !isAudienceEvent(event)) return;
      if (event.type !== 'wordCloud' && event.type !== 'openText') return;
      if (event.slide !== latestSlide.current) return;
      setReads((count) => count + 1);
    });
    let gone = false;
    let retries = 0;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
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
      stopEvents();
      stopUp();
    };
  }, [connection, deckId]);

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
      return api.get<OpenResponseControlView>(responsePath(deckId, at)).then(
        (view) => {
          if (stale()) return;
          setViews((held) => new Map(held).set(at, view));
          setError((shown) => (shown?.slide === at && shown.from === 'read' ? null : shown));
        },
        (failure: unknown) => {
          if (stale()) return;
          if (failure instanceof ApiError && failure.status === 404) {
            markAbsent(at);
            return;
          }
          setError((shown) =>
            shown?.slide === at && shown.from !== 'read'
              ? shown
              : { slide: at, message: sentenceFor(failure), from: 'read' },
          );
        },
      );
    },
    [deckId, markAbsent],
  );

  useEffect(() => {
    if (reads === 0) return;
    let gone = false;
    void read(slide, () => gone);
    return () => {
      gone = true;
    };
  }, [reads, slide, read]);

  const post = useCallback(
    (suffix: string) => async () => {
      const at = latestSlide.current;
      setError(null);
      try {
        arrive(at, await api.post<OpenResponseControlView>(`${responsePath(deckId, at)}${suffix}`));
      } catch (failure) {
        setError({ slide: at, message: sentenceFor(failure), from: 'action' });
        await read(at, () => latestSlide.current !== at);
      }
    },
    [deckId, arrive, read],
  );

  const control = views.get(slide) ?? null;
  const message =
    error?.slide === slide ? error.message : watchFailed && control !== null ? WATCH_FAILED : null;

  return useMemo<OpenResponseControlValue>(
    () => ({
      control,
      open: () => post('/open')(),
      close: () => post('/close')(),
      newRound: () => post('/new-round')(),
      hide: (number) => post(`/entries/${number}/hide`)(),
      restore: (number) => post(`/entries/${number}/restore`)(),
      error: message,
    }),
    [control, message, post],
  );
}

// The panel and the strip read the provider for deckId. Anywhere else is a wiring mistake.
export function useOpenResponsePanel(deckId: string): OpenResponseControlValue {
  const source = useContext(OpenResponseControlContext);
  if (source === null)
    throw new Error('useOpenResponsePanel needs an open-response control provider');
  if (source.deckId !== deckId)
    throw new Error(
      `useOpenResponsePanel(${deckId}) is under the provider for deck ${source.deckId}`,
    );
  return source.value;
}

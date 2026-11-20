import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { AudienceOpenResponses, AudienceStatus } from '../guest/useAudience';
import { openResponseErrorSentence } from './errors';
import type {
  GuestResponseView,
  OpenTextEvent,
  StoredText,
  StoredWords,
  TextRequest,
  WordCloudEvent,
  WordsRequest,
} from './types';

// The audience connection's word-cloud and open-text events, provided by GuestLive. Null outside a guest page: a card
// there shows only what its GET returns.
export const AudienceOpenResponsesContext = createContext<AudienceOpenResponses | null>(null);

export type RoomEvent = WordCloudEvent | OpenTextEvent;

export type OpenResponse = {
  // The guest's view from GET /api/join/{code}/slides/{slide}/response, with their stored entry after each accepted send.
  // Null until the first read answers, and while a read has failed.
  view: GuestResponseView | null;
  // The newest audience event for this slide, when one has arrived since the card mounted.
  room: RoomEvent | null;
  // The sentence for the last failed call (errors.ts), or null.
  error: string | null;
  sending: boolean;
  // PUT .../words or .../answer for the round the view holds. Resolves true once the server stored the entry.
  sendWords(words: string[]): Promise<boolean>;
  sendText(text: string): Promise<boolean>;
};

type Held<T> = { key: string; value: T };

function sentenceOf(error: unknown): string {
  return openResponseErrorSentence(error instanceof ApiError ? error.code : undefined);
}

// The phone's one hook for a word-cloud or open-text slide (SW-8 D9, D13). While status is live it subscribes to the
// slide's audience events, then GETs the guest's view, again after each reconnect, so no event falls between the read
// and the subscription and nothing missed while offline shows. One send is in flight at a time.
export function useOpenResponse(code: string, slide: number, status: AudienceStatus): OpenResponse {
  const audience = useContext(AudienceOpenResponsesContext);
  const key = `${code}:${slide}`;
  const base = `/api/join/${encodeURIComponent(code)}/slides/${slide}`;
  const [view, setView] = useState<Held<GuestResponseView> | null>(null);
  const [room, setRoom] = useState<Held<RoomEvent> | null>(null);
  const [error, setError] = useState<Held<string | null> | null>(null);
  const [sending, setSending] = useState(false);
  const live = status === 'live';

  useEffect(() => {
    if (!live) return;
    let gone = false;
    const take = (event: RoomEvent) => {
      if (!gone && event.slide === slide) setRoom({ key, value: event });
    };
    const stopWords = audience?.onWordCloud(take);
    const stopText = audience?.onOpenText(take);
    api.get<GuestResponseView>(`${base}/response`).then(
      (value) => {
        if (gone) return;
        setView({ key, value });
        setError({ key, value: null });
      },
      (failure: unknown) => {
        if (gone) return;
        setView(null);
        setError({ key, value: sentenceOf(failure) });
      },
    );
    return () => {
      gone = true;
      stopWords?.();
      stopText?.();
    };
  }, [live, audience, base, key, slide]);

  const current = view?.key === key ? view.value : null;

  const send = useCallback(
    async <T>(path: string, body: unknown, stored: (value: T) => GuestResponseView) => {
      setSending(true);
      try {
        const value = await api.put<T>(`${base}/${path}`, body);
        setView({ key, value: stored(value) });
        setError({ key, value: null });
        return true;
      } catch (failure) {
        setError({ key, value: sentenceOf(failure) });
        return false;
      } finally {
        setSending(false);
      }
    },
    [base, key],
  );

  const sendWords = useCallback(
    (words: string[]) => {
      if (current?.kind !== 'word-cloud' || current.phase === 'draft' || sending)
        return Promise.resolve(false);
      const body: WordsRequest = { round: current.round, revision: current.revision, words };
      return send<StoredWords>('words', body, (stored) => ({
        ...current,
        mineWords: stored.words,
      }));
    },
    [current, send, sending],
  );

  const sendText = useCallback(
    (text: string) => {
      if (current?.kind !== 'open-text' || current.phase === 'draft' || sending)
        return Promise.resolve(false);
      const body: TextRequest = { round: current.round, revision: current.revision, text };
      return send<StoredText>('answer', body, (stored) => ({ ...current, mineText: stored.text }));
    },
    [current, send, sending],
  );

  return {
    view: current,
    room: room?.key === key ? room.value : null,
    error: error?.key === key ? error.value : null,
    sending,
    sendWords,
    sendText,
  };
}

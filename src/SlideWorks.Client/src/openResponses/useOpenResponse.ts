import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { AudienceOpenResponses, AudienceStatus } from '../guest/useAudience';
import { OPEN_RESPONSE_REREAD, openResponseErrorSentence } from './errors';
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

type RoundFields = { phase: GuestResponseView['phase']; number: number; revision: number };

// Whether next is the same round as held or a later one: a higher number, or the same number at a higher revision, or at
// the same revision unless a draft would replace an open or closed round.
export function newerRound(next: RoundFields, held: RoundFields): boolean {
  if (next.number !== held.number) return next.number > held.number;
  if (next.revision !== held.revision) return next.revision > held.revision;
  return next.phase !== 'draft' || held.phase === 'draft';
}

const KIND_OF_EVENT = { wordCloud: 'word-cloud', openText: 'open-text' } as const;

// The view with the round fields of a newer event of its kind. A new round starts with no stored entry.
function advance(view: GuestResponseView, event: RoomEvent): GuestResponseView {
  if (KIND_OF_EVENT[event.type] !== view.kind || !newerRound(event, view)) return view;
  const fresh = event.number > view.number;
  const round =
    event.phase === 'draft' ? { phase: event.phase } : { phase: event.phase, round: event.round };
  const fields = {
    ...round,
    question: view.question,
    number: event.number,
    revision: event.revision,
    revealed: event.revealed,
    guests: event.guests,
  };
  return view.kind === 'word-cloud'
    ? { ...fields, kind: view.kind, mineWords: fresh ? [] : view.mineWords }
    : { ...fields, kind: view.kind, mineText: fresh ? '' : view.mineText };
}

// The phone's one hook for a word-cloud or open-text slide (SW-8 D9, D13). While status is live it subscribes to the
// slide's audience events, then GETs the guest's view, again after each reconnect, so no event falls between the read
// and the subscription and nothing missed while offline shows. Each newer event moves the view to its round. A stale or
// wrong_phase refusal reads the view again and keeps its sentence. One send, with its reread, is in flight at a time.
export function useOpenResponse(code: string, slide: number, status: AudienceStatus): OpenResponse {
  const audience = useContext(AudienceOpenResponsesContext);
  const key = `${code}:${slide}`;
  const base = `/api/join/${encodeURIComponent(code)}/slides/${slide}`;
  const [view, setView] = useState<Held<GuestResponseView> | null>(null);
  const [room, setRoom] = useState<Held<RoomEvent> | null>(null);
  const [error, setError] = useState<Held<string | null> | null>(null);
  const [sending, setSending] = useState(false);
  // Moves on each effect cleanup and each send, so a send's late answer never lands on a later slide, connection or send.
  const generation = useRef(0);
  const live = status === 'live';

  // A read never takes the view back to an older round than one an event already brought.
  const accept = useCallback(
    (value: GuestResponseView) =>
      setView((held) =>
        held?.key === key && !newerRound(value, held.value) ? held : { key, value },
      ),
    [key],
  );

  useEffect(() => {
    if (!live) return;
    let gone = false;
    // The newest event so far, laid over the read when it answers: before the first read there is no view to advance.
    let pending: RoomEvent | null = null;
    const take = (event: RoomEvent) => {
      if (gone || event.slide !== slide) return;
      if (pending === null || newerRound(event, pending)) pending = event;
      setRoom((held) =>
        held?.key === key && !newerRound(event, held.value) ? held : { key, value: event },
      );
      setView((held) => (held?.key === key ? { key, value: advance(held.value, event) } : held));
    };
    const stopWords = audience?.onWordCloud(take);
    const stopText = audience?.onOpenText(take);
    api.get<GuestResponseView>(`${base}/response`).then(
      (value) => {
        if (gone) return;
        accept(pending ? advance(value, pending) : value);
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
      generation.current += 1;
      stopWords?.();
      stopText?.();
    };
  }, [live, audience, base, key, slide, accept]);

  const current = view?.key === key ? view.value : null;

  // store merges the stored entry into the newest view held, and only while that view is still the round that was sent.
  const send = useCallback(
    async <T>(
      path: string,
      round: string,
      body: unknown,
      store: (held: GuestResponseView, value: T) => GuestResponseView,
    ) => {
      const mine = ++generation.current;
      const ours = () => generation.current === mine;
      setSending(true);
      try {
        const value = await api.put<T>(`${base}/${path}`, body);
        setView((held) =>
          held?.key === key && held.value.round === round
            ? { key, value: store(held.value, value) }
            : held,
        );
        if (ours()) setError({ key, value: null });
        return true;
      } catch (failure) {
        if (!ours()) return false;
        setError({ key, value: sentenceOf(failure) });
        if (failure instanceof ApiError && OPEN_RESPONSE_REREAD.includes(failure.code ?? '')) {
          try {
            const reread = await api.get<GuestResponseView>(`${base}/response`);
            if (ours()) accept(reread);
          } catch (reread) {
            if (ours()) setError({ key, value: sentenceOf(reread) });
          }
        }
        return false;
      } finally {
        setSending(false);
      }
    },
    [base, key, accept],
  );

  const sendWords = useCallback(
    (words: string[]) => {
      if (current?.kind !== 'word-cloud' || current.phase !== 'open' || sending)
        return Promise.resolve(false);
      const body: WordsRequest = { round: current.round, revision: current.revision, words };
      return send<StoredWords>('words', current.round, body, (held, stored) =>
        held.kind === 'word-cloud' ? { ...held, mineWords: stored.words } : held,
      );
    },
    [current, send, sending],
  );

  const sendText = useCallback(
    (text: string) => {
      if (current?.kind !== 'open-text' || current.phase !== 'open' || sending)
        return Promise.resolve(false);
      const body: TextRequest = { round: current.round, revision: current.revision, text };
      return send<StoredText>('answer', current.round, body, (held, stored) =>
        held.kind === 'open-text' ? { ...held, mineText: stored.text } : held,
      );
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

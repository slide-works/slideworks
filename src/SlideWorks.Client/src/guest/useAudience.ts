import { HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { OpenTextEvent, WordCloudEvent } from '../openResponses/types';
import type { PollTally } from '../polls/types';
import type { QuestionTally } from '../qa/types';
import { createAudienceConnection } from '../realtime/audienceConnection';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent, type AudienceState } from './audienceEvents';
import { NO_SESSION, TOO_MANY_ATTEMPTS, type GuestView } from './guestView';
import { languageQuery } from './language/GuestLanguageContext';

// needs_cookies: the hub refused with no_guest, so the browser isn't keeping the guest cookie.
export type AudienceStatus = 'connecting' | 'live' | 'reconnecting' | 'offline' | 'needs_cookies';

// How long after a failed view re-read the hook tries it again, and after a too_many_attempts refusal (a whole venue shares
// one rate-limit key, so the slower wait gives the limit time to clear). Each wait gets a random extra so phones don't all
// call the server in the same instant: up to JITTER_MS on a rejoin or re-read, up to SLOW_JITTER_MS on the slow wait.
export const REREAD_RETRY_MS = 3000;
export const THROTTLED_RETRY_MS = 5000;
export const JITTER_MS = 1000;
export const SLOW_JITTER_MS = 5000;

// The random source, injected so tests are deterministic. It returns a number in [0, 1).
export type Random = () => number;

// The poll's side of the audience connection (SW-6 V5), for the guest's poll card. Its identity never changes.
// - onTally: calls listener with every pollTally the connection receives, in order, one call each (the results group
//   gets two per change in one tick). It returns the unsubscribe. Only tallies that arrive after subscribing are
//   delivered. Order on mount and after each reconnect: subscribe, then watchPoll when it applies, then GET
//   /api/join/{code}/poll, so no tally falls between the read and the subscription.
// - watchPoll: invokes WatchPoll(code) on the connection that receives the tallies. It resolves once the hub has added
//   the connection to the poll's results group, and rejects with the hub's refusal (an Error whose message ends
//   "HubException: not_answered") or the connection's own failure. The hub forgets the group when the connection drops,
//   so call it again each time status comes back to 'live' after 'reconnecting'.
export type AudiencePolls = {
  onTally(listener: (tally: PollTally) => void): () => void;
  watchPoll(): Promise<void>;
};

// The questions' side of the audience connection (SW-9 Q5, Q8), for the phone's Questions tab. Its identity never changes.
// - onTally: calls listener with every questionTally the connection receives, in order, one call each, and returns the
//   unsubscribe. Only tallies that arrive after subscribing are delivered, so subscribe before GET
//   /api/join/{code}/questions. The room (audienceState.room) is on state, not here.
export type AudienceQuestions = {
  onTally(listener: (tally: QuestionTally) => void): () => void;
};

// The word-cloud and open-text side of the audience connection (SW-8 D9), for the phone's cards. Its identity never
// changes. Each on* calls listener with every event of its type the connection receives, in order, and returns the
// unsubscribe. Only events that arrive after subscribing are delivered, so subscribe before GET
// /api/join/{code}/slides/{slide}/response.
export type AudienceOpenResponses = {
  onWordCloud(listener: (event: WordCloudEvent) => void): () => void;
  onOpenText(listener: (event: OpenTextEvent) => void): () => void;
};

export type Audience = {
  view: GuestView;
  state: AudienceState;
  status: AudienceStatus;
  polls: AudiencePolls;
  questions: AudienceQuestions;
  openResponses: AudienceOpenResponses;
  // Moves this phone's slide state to a language (SetLanguage, SW-883 K5) and lands on the state it answers with. It
  // rejects when the hub refuses or the connection is away; the language the phone is in is then unchanged.
  setLanguage(language: string): Promise<void>;
};

// Calls each listener with value, on a copy of the set, so a listener that unsubscribes while being called doesn't skip
// the next one. A listener that throws doesn't stop the others: its error is thrown again on its own, where the page's
// error reporting sees it.
function handToEach<T>(listeners: Set<(value: T) => void>, value: T) {
  for (const listener of [...listeners]) {
    try {
      listener(value);
    } catch (error) {
      queueMicrotask(() => {
        throw error;
      });
    }
  }
}

// The text the real hub's error completion ends with when JoinAudience throws HubException(message) (J4).
const NO_GUEST = 'no_guest';

// SignalR rejects an invoke for two kinds of reason. A server refusal is the hub's own completion, whose message carries
// "HubException:". Anything else is the connection's own failure (an invoke cancelled because the connection closed, or
// made while it isn't connected): the reconnect and close handlers deal with that, so it is not a verdict on the join.
const isHubException = (error: unknown) =>
  error instanceof Error && error.message.includes('HubException:');

const hubRefusal = (error: unknown, message: string) =>
  error instanceof Error && error.message.endsWith(`HubException: ${message}`);

// The guest's realtime state (SW-5 J5). initial is the view GET /api/join/{code} returned; the hook returns the view the
// screens show, which it re-reads when the state's revision changes. It joins the audience on every connection (the first
// and each reconnect) and lands on the state that answers, never replaying what it missed. A refusal the contract names
// (no_session, too_many_attempts) and a connection that has used up its retries both report offline, and no_guest
// reports needs_cookies; any other hub refusal is thrown to the page's error boundary (GuestLive), while a rejection that
// comes from the connection itself is left to the reconnect and close handlers. too_many_attempts never
// takes a guest who already has a view offline: the screen stays, the banner shows, and the call is tried again later. A
// failed view re-read doesn't take the phone offline for a blip or a rate limit, and stops for good once the talk has ended
// or the deck has gone (no_session: offline). A state with the same seq as the held one is applied only when its phase
// differs (lobby to live), and audienceEnded is applied whatever the seq. Re-reads and rejoins wait a random extra
// so a room of phones doesn't hit the server together. startLanguage is the language the phone wants from the first
// join: after JoinAudience it calls SetLanguage when that isn't the primary, again after every reconnect, and the first
// state is held back until SetLanguage answers. A refused SetLanguage lands on the join's own (primary) state. polls hands each pollTally to its subscribers and invokes
// WatchPoll on the same connection (SW-6). questions hands each questionTally to its subscribers (SW-9); the room comes
// with each state.
export function useAudience(
  code: string,
  initial: GuestView,
  random: Random = Math.random,
  startLanguage: string | null = null,
): Audience {
  const [view, setView] = useState(initial);
  const [state, setState] = useState(initial.state);
  const [status, setStatus] = useState<AudienceStatus>('connecting');
  const randomRef = useRef(random);
  useEffect(() => {
    randomRef.current = random;
  });
  const [failure, setFailure] = useState<unknown>(null);
  // The language the phone wants now, which every re-read and every rejoin carries.
  const languageRef = useRef(startLanguage);
  const primaryRef = useRef(initial.translations?.primary ?? null);
  const setLanguageRef = useRef<(language: string) => Promise<void>>(() =>
    Promise.reject(new Error('The audience connection is closed.')),
  );
  useEffect(() => {
    primaryRef.current = view.translations?.primary ?? null;
  });
  const setLanguage = useCallback((language: string) => setLanguageRef.current(language), []);
  // The poll channel reads the connection and code of the running effect; listeners outlive a reconnect.
  const tallyListeners = useRef(new Set<(tally: PollTally) => void>());
  const live = useRef<{ connection: HubConnection; code: string } | null>(null);
  const polls = useMemo<AudiencePolls>(
    () => ({
      onTally: (listener) => {
        tallyListeners.current.add(listener);
        return () => void tallyListeners.current.delete(listener);
      },
      watchPoll: () =>
        live.current
          ? live.current.connection.invoke<void>('WatchPoll', live.current.code)
          : Promise.reject(new Error('The audience connection is closed.')),
    }),
    [],
  );
  const questionListeners = useRef(new Set<(tally: QuestionTally) => void>());
  const questions = useMemo<AudienceQuestions>(
    () => ({
      onTally: (listener) => {
        questionListeners.current.add(listener);
        return () => void questionListeners.current.delete(listener);
      },
    }),
    [],
  );
  const wordCloudListeners = useRef(new Set<(event: WordCloudEvent) => void>());
  const openTextListeners = useRef(new Set<(event: OpenTextEvent) => void>());
  const openResponses = useMemo<AudienceOpenResponses>(
    () => ({
      onWordCloud: (listener) => {
        wordCloudListeners.current.add(listener);
        return () => void wordCloudListeners.current.delete(listener);
      },
      onOpenText: (listener) => {
        openTextListeners.current.add(listener);
        return () => void openTextListeners.current.delete(listener);
      },
    }),
    [],
  );

  // A new code or a new GET (the page's retry) starts again from the view it was given.
  const [seenInitial, setSeenInitial] = useState(initial);
  if (seenInitial !== initial) {
    setSeenInitial(initial);
    setView(initial);
    setState(initial.state);
    setStatus('connecting');
  }

  useEffect(() => {
    let active = true;
    let lastSeq = initial.state.seq;
    let lastPhase = initial.state.phase;
    let viewRevision = initial.state.revision;
    let reading: number | null = null;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    let joinTimer: ReturnType<typeof setTimeout> | undefined;
    // Runs after base plus a random extra of up to spread ms; with nothing to wait it runs now. Returns the timer, if any.
    const later = (run: () => void, base: number, spread: number) => {
      const wait = base + Math.floor(randomRef.current() * spread);
      if (wait > 0) return setTimeout(run, wait);
      run();
      return undefined;
    };
    // Set when a pushed state is applied while a JoinAudience call is in flight: the answer is then the older one.
    let pushedSinceJoin = false;
    const connection = createAudienceConnection();
    const mine = { connection, code };
    live.current = mine;

    const offline = () => {
      if (active) setStatus('offline');
    };

    const reread = (revision: number) => {
      // Once the talk has ended the view has nothing more to say, so there is nothing to read again.
      if (lastPhase === 'ended' || reading === revision || viewRevision === revision) return;
      reading = revision;
      const again = (base: number, spread: number) => {
        clearTimeout(retryTimer);
        retryTimer = later(() => reread(revision), base, spread);
      };
      // The read waits a random extra, so a revision change reaching a room doesn't send every phone at once.
      clearTimeout(retryTimer);
      retryTimer = later(
        () =>
          void api
            .get<GuestView>(
              `/api/join/${encodeURIComponent(code)}${languageQuery(languageRef.current)}`,
            )
            .then(
              (next) => {
                if (!active) return;
                if (reading === revision) reading = null;
                // Two reads can finish out of order: never go back to an older view.
                if (next.state.revision < viewRevision) return;
                viewRevision = next.state.revision;
                setView(next);
              },
              (error: unknown) => {
                // A newer revision's read has taken over from this one: it does the retrying.
                if (!active || reading !== revision) return;
                reading = null;
                // The deck is gone (deleted mid-talk): there is nothing to read again, so the phone goes offline.
                if (error instanceof ApiError && error.message === NO_SESSION) offline();
                // The hub decides whether the phone is offline, so any other failure is only tried again: soon after a
                // blip, later after a rate limit.
                else if (error instanceof ApiError && error.message === TOO_MANY_ATTEMPTS)
                  again(THROTTLED_RETRY_MS, SLOW_JITTER_MS);
                else again(REREAD_RETRY_MS, JITTER_MS);
              },
            ),
        0,
        JITTER_MS,
      );
    };

    // A state from the hub is applied only when it is newer than the last one.
    const apply = (next: AudienceState) => {
      if (!active) return;
      // Seqs only rise, but a state with the held seq and another phase (the lobby going live) is not a repeat. The server
      // is being fixed never to send equal seqs, so the rule stays this small.
      if (next.seq < lastSeq || (next.seq === lastSeq && next.phase === lastPhase)) return;
      lastSeq = next.seq;
      lastPhase = next.phase;
      pushedSinceJoin = true;
      setState(next);
      reread(next.revision);
    };

    // The answer to a (re)join is where the guest lands, whatever its seq: it is the server's current state, and the server
    // may have restarted (D2). The exception is a state pushed while the call was in flight: that one is at least as new as
    // the answer, which was read before it, so an answer with the same seq (Go live racing the join) doesn't replace it.
    const land = (next: AudienceState) => {
      if (!active) return;
      if (pushedSinceJoin && next.seq <= lastSeq) {
        setStatus('live');
        return;
      }
      lastSeq = next.seq;
      lastPhase = next.phase;
      setState(next);
      setStatus('live');
      reread(next.revision);
    };

    // The answer to SetLanguage: the same move's state in another language, so an equal seq is applied.
    const adopt = (next: AudienceState) => {
      if (!active || next.seq < lastSeq) return;
      lastSeq = next.seq;
      lastPhase = next.phase;
      setState(next);
      reread(next.revision);
    };
    setLanguageRef.current = async (language) => {
      if (!active || connection.state !== HubConnectionState.Connected)
        throw new Error('The audience connection is closed.');
      const next = await connection.invoke<AudienceState>('SetLanguage', code, language);
      languageRef.current = language;
      adopt(next);
    };

    // JoinAudience, then SetLanguage when the phone wants a language other than the primary. The first render waits for
    // the second answer.
    const joinInLanguage = async (): Promise<void> => {
      const joined = await connection.invoke<AudienceState>('JoinAudience', code);
      const wanted = languageRef.current;
      if (!wanted || wanted === primaryRef.current) return land(joined);
      // A refusal here goes through the join's own error path, like JoinAudience's: the pick is never dropped.
      land(await connection.invoke<AudienceState>('SetLanguage', code, wanted));
    };

    const join = (): void => {
      // Only a connected connection can join. If it dropped again meanwhile, onreconnected or onclose takes it from there.
      if (!active || connection.state !== HubConnectionState.Connected) return;
      pushedSinceJoin = false;
      void joinInLanguage().then(undefined, (error: unknown) => {
        if (!active) return;
        // The connection's own failure, not the server's answer: left to the reconnect and close handlers.
        if (!isHubException(error)) return;
        if (hubRefusal(error, NO_SESSION)) offline();
        else if (hubRefusal(error, TOO_MANY_ATTEMPTS)) {
          // A venue shares one limit. The guest keeps the screen, the banner shows, and the join is tried again later.
          setStatus('reconnecting');
          clearTimeout(joinTimer);
          joinTimer = later(join, THROTTLED_RETRY_MS, SLOW_JITTER_MS);
        } else if (hubRefusal(error, NO_GUEST)) {
          setStatus('needs_cookies');
        } else setFailure(error);
      });
    };

    connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (!isAudienceEvent(event)) return;
      if (event.type === 'audienceState') apply(event);
      else if (event.type === 'audienceEnded' && active) {
        // Whatever the seq: the ended answers can carry a lower one than the last live state.
        lastPhase = 'ended';
        setState((current) => ({ ...current, phase: 'ended', current: null }));
      } else if (event.type === 'pollTally' && active) handToEach(tallyListeners.current, event);
      else if (event.type === 'questionTally' && active)
        handToEach(questionListeners.current, event);
      else if (event.type === 'wordCloud' && active) handToEach(wordCloudListeners.current, event);
      else if (event.type === 'openText' && active) handToEach(openTextListeners.current, event);
      // pollControl, questionControl and reactions go only to the presenter's screens: a guest never receives them, so
      // nothing reads them here.
    });
    connection.onreconnecting(() => {
      // A rejoin still waiting belongs to the connection that just dropped; onreconnected schedules the next.
      clearTimeout(joinTimer);
      if (active) setStatus('reconnecting');
    });
    // Phones that reconnect together rejoin a random moment apart.
    connection.onreconnected(() => {
      clearTimeout(joinTimer);
      joinTimer = later(join, 0, JITTER_MS);
    });
    connection.onclose(() => {
      clearTimeout(joinTimer);
      clearTimeout(retryTimer);
      offline();
    });
    void connection.start().then(join, offline);

    return () => {
      active = false;
      if (live.current === mine) live.current = null;
      clearTimeout(retryTimer);
      clearTimeout(joinTimer);
      void connection.stop();
    };
  }, [code, initial]);

  if (failure) throw failure;
  return { view, state, status, polls, questions, openResponses, setLanguage };
}

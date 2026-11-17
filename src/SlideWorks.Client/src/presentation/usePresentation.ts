import { useCallback, useEffect, useRef, useState } from 'react';
import { DECK_NOT_FOUND } from '../realtime/hubErrors';
import {
  acquireDeckConnection,
  RETRY_DELAYS_MS,
  type DeckConnectionLease,
} from '../realtime/useDeckConnection';
import type {
  PresentationJoined,
  PresentationResume,
  PresentationRole,
  PresentationState,
  PresentationUpdate,
  TimerCommand,
} from './types';

// The server side is DeckHub's presentation calls (contract P2). Every window and phone renders the state the server
// sends and never keeps a position of its own (D4).
const STATE_METHOD = 'presentationState';
// HubException("Deck not found"), as the SignalR client reports it: the join is refused (D3), so there is nothing to
// retry. Any other failure is retried with back-off while the connection is up, and by the next reconnect.
const REFUSED = `HubException: ${DECK_NOT_FOUND}`;
const TICK_MS = 1000;
const MINUTES_PER_DAY = 1440;

export type PresentationStatus = 'connecting' | 'live' | 'reconnecting' | 'refused';

export interface Presentation {
  status: PresentationStatus;
  state: PresentationState | null;
  // The timer's elapsed time now: the server's elapsedMs, plus the performance.now() time since it arrived while the
  // timer is running. 0 before the first state.
  elapsedMs: number;
  // Minutes since local midnight in the browser's time zone (0-1439), from the last join, advanced with
  // performance.now(). null before the first join.
  clockMinutes: number | null;
  // Sends a new position. Does nothing (and resolves) unless live; rejects when the server or the connection fails.
  update(u: PresentationUpdate): Promise<void>;
  // Pauses, resumes or resets the timer. As update: nothing unless live, rejects on failure.
  timer(cmd: TimerCommand): Promise<void>;
}

// What the hook has applied, and when it arrived on the performance.now() clock.
type Applied = { state: PresentationState; at: number };
type Clock = { minutes: number; at: number };

let pageClientId: string | undefined;

// One random id per page load (P1). crypto.randomUUID is missing on plain HTTP, so fall back to 16 random bytes.
function clientId(): string {
  if (pageClientId === undefined) {
    pageClientId =
      typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) =>
            b.toString(16).padStart(2, '0'),
          ).join('');
  }
  return pageClientId;
}

// A state is applied when its epoch differs from the last applied one, or when the epoch is the same and its seq is
// higher (P1), so a late join result never rolls a window back.
function newer(next: PresentationState, last: PresentationState | undefined): boolean {
  return last === undefined || next.epoch !== last.epoch || next.seq > last.seq;
}

function elapsedAt(applied: Applied | null, now: number): number {
  if (!applied) return 0;
  const { timer } = applied.state;
  return timer.state === 'running'
    ? timer.elapsedMs + Math.max(0, now - applied.at)
    : timer.elapsedMs;
}

function clockAt(clock: Clock | null, now: number): number | null {
  if (!clock) return null;
  const passed = Math.floor(Math.max(0, now - clock.at) / 60_000);
  return (clock.minutes + passed) % MINUTES_PER_DAY;
}

function isRefusal(error: unknown): boolean {
  return error instanceof Error && error.message.endsWith(REFUSED);
}

// Joins the deck's presentation on the page's shared hub connection and follows its state (contract P6). It rejoins
// after every reconnect and whenever the page becomes visible again, sending the last applied state as resume, so a
// server restart carries on from this window (D9). A join that fails is retried with back-off; a refused join is
// final: no retry. On unmount (or a new deck) it sends LeavePresentation when the connection stays open for the rest
// of the page, so the window doesn't linger in the presentation's connected list.
export function usePresentation(
  deckId: string,
  role: PresentationRole,
  label: string,
): Presentation {
  const [status, setStatus] = useState<PresentationStatus>('connecting');
  const [applied, setApplied] = useState<Applied | null>(null);
  const [clock, setClock] = useState<Clock | null>(null);
  const [now, setNow] = useState(0);
  // The same values for the callbacks, which outlive a render.
  const last = useRef<Applied | null>(null);
  const live = useRef(false);
  // The effect's lease on the shared connection, for update and timer.
  const leaseRef = useRef<DeckConnectionLease | null>(null);

  useEffect(() => {
    if (deckId === '') return;
    // Taken here rather than with useDeckConnection, so the cleanup below can still send LeavePresentation on it.
    const lease = acquireDeckConnection();
    const leaveGroup = lease.member(`present:${deckId}`);
    leaseRef.current = lease;
    let gone = false;
    let refused = false;
    let joinedOnce = false;
    let up = false;
    let failures = 0;
    let retry: ReturnType<typeof setTimeout> | undefined;
    last.current = null;
    live.current = false;

    const apply = (state: PresentationState) => {
      if (gone || state.deckId !== deckId || !newer(state, last.current?.state)) return;
      const at = performance.now();
      last.current = { state, at };
      setApplied(last.current);
      setNow(at);
    };

    const resume = (): PresentationResume | null => {
      const previous = last.current;
      if (!previous) return null;
      const { slide, step, black, skipDeepDives, timer } = previous.state;
      return {
        slide,
        step,
        black,
        skipDeepDives,
        timer: { state: timer.state, elapsedMs: elapsedAt(previous, performance.now()) },
      };
    };

    const join = async () => {
      if (gone || refused) return;
      clearTimeout(retry);
      retry = undefined;
      const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
      let joined: PresentationJoined;
      try {
        joined = await lease.invoke<PresentationJoined>(
          'JoinPresentation',
          deckId,
          clientId(),
          role,
          label,
          timeZone,
          resume(),
        );
      } catch (error) {
        if (gone) return;
        if (isRefusal(error)) {
          refused = true;
          live.current = false;
          setStatus('refused');
          return;
        }
        // Not a refusal (the server failed, or the connection went mid-call): try again with back-off while the
        // connection is up. A connection that is down rejoins when it comes back up.
        if (up) {
          const delay = RETRY_DELAYS_MS[Math.min(failures, RETRY_DELAYS_MS.length - 1)];
          failures += 1;
          clearTimeout(retry);
          retry = setTimeout(() => void join(), delay);
        }
        return;
      }
      if (gone) return;
      failures = 0;
      apply(joined.state);
      const at = performance.now();
      setClock({ minutes: joined.clockMinutes, at });
      setNow(at);
      joinedOnce = true;
      live.current = true;
      setStatus('live');
    };

    const stopListening = lease.on(STATE_METHOD, (state: unknown) =>
      apply(state as PresentationState),
    );
    const stopUp = lease.onUp(() => {
      up = true;
      failures = 0;
      void join();
    });
    const stopDown = lease.onDown(() => {
      up = false;
      clearTimeout(retry);
      retry = undefined;
      if (refused) return;
      live.current = false;
      setStatus(joinedOnce ? 'reconnecting' : 'connecting');
    });
    // performance.now() can stall while a phone sleeps, so a page coming back refreshes the clock and the state. It
    // also joins a page whose join failed, without waiting for the next retry.
    const onVisible = () => {
      if (document.visibilityState === 'visible' && up && !refused) void join();
    };
    document.addEventListener('visibilitychange', onVisible);

    return () => {
      gone = true;
      live.current = false;
      clearTimeout(retry);
      stopListening();
      stopUp();
      stopDown();
      document.removeEventListener('visibilitychange', onVisible);
      // A refused join never joined. A failed leave changes nothing: the server drops the window when the page closes.
      if (leaveGroup() && !refused) {
        void lease.invoke('LeavePresentation', deckId).catch(() => undefined);
      }
      lease.release();
      if (leaseRef.current === lease) leaseRef.current = null;
      setStatus('connecting');
      setApplied(null);
      setClock(null);
    };
  }, [deckId, role, label]);

  // A 1-second tick moves the timer and the clock on while there is something to show.
  useEffect(() => {
    if (!applied && !clock) return;
    const tick = setInterval(() => setNow(performance.now()), TICK_MS);
    return () => clearInterval(tick);
  }, [applied, clock]);

  const call = useCallback(
    async (method: string, ...args: unknown[]) => {
      const lease = leaseRef.current;
      if (!live.current || !lease) return;
      const state = await lease.invoke<PresentationState>(method, deckId, ...args);
      // The broadcast carries the same state; whichever comes first applies.
      if (state.deckId !== deckId || !newer(state, last.current?.state)) return;
      const at = performance.now();
      last.current = { state, at };
      setApplied(last.current);
      setNow(at);
    },
    [deckId],
  );
  const update = useCallback((u: PresentationUpdate) => call('UpdatePresentation', u), [call]);
  const timer = useCallback((cmd: TimerCommand) => call('SetTimer', cmd), [call]);

  return {
    status,
    state: applied?.state ?? null,
    elapsedMs: elapsedAt(applied, now),
    clockMinutes: clockAt(clock, now),
    update,
    timer,
  };
}

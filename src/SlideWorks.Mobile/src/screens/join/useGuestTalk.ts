import { ApiError } from '@slideworks/core/client';
import { getJoin, isGuestId, type JoinSummary } from '@slideworks/core/guest';
import { createHubConnection, type HubConnectionOptions } from '@slideworks/core/realtime/hub';
import type { AudienceEventSummary, AudienceStateSummary } from '@slideworks/core/realtime/shapes';
import { useEffect, useState } from 'react';
import { useAppRuntime } from '../../platform/appRuntime';
import { isJoinRefusal, type JoinRefusal } from './refusals';

// HttpTransportType.WebSockets | ServerSentEvents | LongPolling, as the web's audience connection offers
// (src/SlideWorks.Client/src/realtime/audienceConnection.ts): a guest may sit behind a network that blocks WebSockets.
// The app doesn't depend on @microsoft/signalr itself, so the enum's values are written out.
export const GUEST_TRANSPORTS = (1 | 2 | 4) as HubConnectionOptions['transport'];

// The web's back-off (useDeckConnection's RETRY_DELAYS_MS); after one pass SignalR stops and the screen shows offline.
const RETRY_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000];
export const guestRetryDelayMs = (previousRetryCount: number): number | null =>
  RETRY_DELAYS_MS[previousRetryCount] ?? null;

const AUDIENCE_EVENT = 'audienceEvent';

export type GuestTalk =
  | { status: 'joining' }
  | { status: 'refused'; refusal: JoinRefusal }
  | { status: 'joined'; view: JoinSummary; state: AudienceStateSummary; offline: boolean }
  | { status: 'failed'; error: unknown };

// The hub's refusal of JoinAudience ends "HubException: <code>" (as useAudience.ts reads it on the web).
function hubRefusal(error: unknown): JoinRefusal | null {
  if (!(error instanceof Error)) return null;
  const code = /HubException: (\w+)$/.exec(error.message)?.[1];
  return isJoinRefusal(code) ? code : null;
}

const isState = (event: AudienceEventSummary): event is AudienceStateSummary =>
  event.type === 'audienceState';

// Joins the talk with that code as a guest of origin (contract K7) and follows its slide over the deck hub (K3). The
// join comes first: an id the server issues is stored for that origin before the hub connection is built, because the
// connection reads the guest id once, when it is built. The guest id goes only to origin, and never the bearer.
export function useGuestTalk(origin: string, code: string): GuestTalk {
  const { guests, guestId } = useAppRuntime();
  const [talk, setTalk] = useState<GuestTalk>({ status: 'joining' });

  useEffect(() => {
    let active = true;
    let stop: (() => void) | null = null;
    setTalk({ status: 'joining' });

    const joined = (view: JoinSummary, state: AudienceStateSummary, offline = false) =>
      active && setTalk({ status: 'joined', view, state, offline });

    async function run() {
      let view: JoinSummary;
      try {
        const result = await getJoin(origin, code);
        view = result.view;
        if (result.issuedGuestId !== null && isGuestId(result.issuedGuestId))
          guests.remember(origin, result.issuedGuestId);
      } catch (error) {
        if (!active) return;
        if (error instanceof ApiError && isJoinRefusal(error.code))
          setTalk({ status: 'refused', refusal: error.code });
        else setTalk({ status: 'failed', error });
        return;
      }
      if (!active) return;

      // Seqs only rise; a state with the held seq and another phase (the lobby going live) is not a repeat.
      let held = view.state;
      joined(view, held);
      if (held.phase === 'ended') return;

      const connection = createHubConnection({
        auth: { kind: 'guest', origin, guestId: () => guestId(origin) },
        transport: GUEST_TRANSPORTS,
        nextRetryDelayMs: guestRetryDelayMs,
      });
      stop = () => void connection.stop();

      // Set when a push is applied while a JoinAudience call is in flight: the answer was read before it, so it is the
      // older one (as useAudience.ts on the web does).
      let pushedSinceJoin = false;
      const land = (next: AudienceStateSummary) => {
        held = next;
        joined(view, next);
        if (next.phase === 'ended') stop?.();
      };
      const join = async () => {
        pushedSinceJoin = false;
        try {
          const answer = await connection.invoke<AudienceStateSummary>('JoinAudience', code);
          // The answer is where the guest lands, whatever its seq (the server may have restarted), unless a state was
          // pushed meanwhile that is at least as new.
          if (pushedSinceJoin && answer.seq <= held.seq) return;
          land(answer);
        } catch (error) {
          if (!active) return;
          const refusal = hubRefusal(error);
          if (refusal) setTalk({ status: 'refused', refusal });
          else if (error instanceof Error && error.message.includes('HubException:'))
            setTalk({ status: 'failed', error });
          // Anything else is the connection's own failure, which onclose and onreconnected deal with.
        }
      };

      connection.on(AUDIENCE_EVENT, (event: AudienceEventSummary) => {
        if (!active) return;
        if (event.type === 'audienceEnded') {
          pushedSinceJoin = true;
          land({ ...held, phase: 'ended', current: null });
        } else if (isState(event)) {
          if (event.seq < held.seq || (event.seq === held.seq && event.phase === held.phase))
            return;
          pushedSinceJoin = true;
          land(event);
        }
      });
      connection.onreconnecting(() => joined(view, held, true));
      connection.onreconnected(() => void join());
      connection.onclose(() => {
        if (active && held.phase !== 'ended') joined(view, held, true);
      });

      try {
        await connection.start();
      } catch {
        if (active) joined(view, held, true);
        return;
      }
      if (!active) {
        void connection.stop();
        return;
      }
      await join();
    }

    void run();
    return () => {
      active = false;
      stop?.();
    };
  }, [origin, code, guests, guestId]);

  return talk;
}

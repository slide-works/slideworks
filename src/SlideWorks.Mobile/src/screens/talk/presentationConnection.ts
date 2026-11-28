import { createHubConnection, type HubConnectionOptions } from '@slideworks/core/realtime/hub';
import type {
  PresentationJoinedSummary,
  PresentationStateSummary,
} from '@slideworks/core/realtime/shapes';
import type { SessionStore } from '../../session/sessionStore';

// Follows one deck's presentation as a phone (D15): joins over the deck hub with the device bearer, as the web's phone
// remote does (SlideWorks.Client/src/presentation/usePresentation.ts), rejoins after every reconnect, and reports the
// slide the server sends. A refused join (the deck isn't the person's, or is gone) is final.

export type PresentationStatus = 'connecting' | 'live' | 'reconnecting' | 'refused';

export type PresentationListener = {
  status(status: PresentationStatus): void;
  state(state: PresentationStateSummary): void;
};

export type PresentationTarget = {
  deckId: string;
  clientId: string;
  label: string;
  timeZone: string;
};

// HttpTransportType.WebSockets, as the web's deck connection uses (the app doesn't depend on @microsoft/signalr).
const WEB_SOCKETS = 1 as HubConnectionOptions['transport'];
// The web's deck connection delays (SlideWorks.Client/src/realtime/useDeckConnection.ts): keep trying, at most every 30 s.
export const RETRY_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000];
const STATE_EVENT = 'presentationState';
// DeckHub.DECK_NOT_FOUND (src/SlideWorks.WebApi/Realtime/DeckHub.cs), as the SignalR client reports a refused join.
const REFUSED = 'HubException: Deck not found';

const retryDelay = (attempt: number) =>
  RETRY_DELAYS_MS[Math.min(attempt, RETRY_DELAYS_MS.length - 1)];

export function isRefusal(error: unknown): boolean {
  return error instanceof Error && error.message.endsWith(REFUSED);
}

// The secret goes only to the server the person signed in to: the hub URL is apiOrigin(), and the secret is the one
// bound to that origin.
export function bearerFor(session: SessionStore, apiOrigin: () => string): () => string | null {
  return () => session.secretFor(apiOrigin());
}

// Opens the connection and joins; returns the function that leaves and closes it.
export function followPresentation(
  target: PresentationTarget,
  secret: () => string | null,
  listener: PresentationListener,
): () => void {
  const connection = createHubConnection({
    auth: { kind: 'bearer', secret },
    transport: WEB_SOCKETS,
    nextRetryDelayMs: retryDelay,
  });
  let gone = false;
  let refused = false;
  let startTimer: ReturnType<typeof setTimeout> | undefined;
  let latestJoin = 0;

  const report = (status: PresentationStatus) => {
    if (!gone && !refused) listener.status(status);
  };

  const join = async () => {
    const thisJoin = ++latestJoin;
    try {
      const joined = await connection.invoke<PresentationJoinedSummary>(
        'JoinPresentation',
        target.deckId,
        target.clientId,
        'phone',
        target.label,
        target.timeZone,
        null,
      );
      if (gone || thisJoin !== latestJoin) return;
      listener.state(joined.state);
      report('live');
    } catch (error) {
      if (gone || thisJoin !== latestJoin) return;
      if (isRefusal(error)) {
        listener.status('refused');
        refused = true;
        void connection.stop();
      }
      // Any other failure: the connection is going down, and the next reconnect joins again.
    }
  };

  const start = async (attempt: number) => {
    try {
      await connection.start();
    } catch {
      // A first connect that fails isn't covered by SignalR's automatic reconnect: try again with the same delays.
      if (gone) return;
      report('reconnecting');
      startTimer = setTimeout(() => void start(attempt + 1), retryDelay(attempt + 1));
      return;
    }
    if (!gone) await join();
  };

  connection.on(STATE_EVENT, (state: PresentationStateSummary) => {
    if (!gone && !refused) listener.state(state);
  });
  connection.onreconnecting(() => report('reconnecting'));
  connection.onreconnected(() => void join());
  connection.onclose(() => {
    if (gone || refused) return;
    // Automatic reconnect gave up (it never does with these delays) or the server closed: start again.
    report('reconnecting');
    startTimer = setTimeout(() => void start(0), retryDelay(1));
  });

  report('connecting');
  void start(0);

  return () => {
    gone = true;
    clearTimeout(startTimer);
    void connection.stop();
  };
}

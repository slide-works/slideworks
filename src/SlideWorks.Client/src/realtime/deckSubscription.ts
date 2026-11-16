import {
  HttpTransportType,
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { getToken } from '../api/client';
import type { DeckEvent } from './deckEvents';

// The server side is src/SlideWorks.WebApi/Realtime/DeckHub.cs: JoinDeck(deckId) starts a deck's events, delivered as
// "deckEvent" messages carrying the DeckEvent JSON (deckEvents.ts).
export const DECK_HUB_PATH = '/hubs/deck';
const EVENT_METHOD = 'deckEvent';

// The slice of HubConnection this module uses, so a test can pass a fake.
export type RealtimeConnection = Pick<
  HubConnection,
  'start' | 'stop' | 'invoke' | 'on' | 'onreconnected'
>;

// Seconds-scale back-off, used both for the first connect (SignalR does not retry that) and, through
// withAutomaticReconnect, for a dropped connection. The last delay repeats.
export const RETRY_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000];

// WebSockets only: the hub offers no other transport. The token goes on the negotiate request as a bearer header and,
// since a browser can't set headers on a WebSocket, on the upgrade as ?access_token= (accepted on /hubs only).
export function createDeckConnection(): RealtimeConnection {
  return new HubConnectionBuilder()
    .withUrl(DECK_HUB_PATH, {
      transport: HttpTransportType.WebSockets,
      accessTokenFactory: () => getToken() ?? '',
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
        RETRY_DELAYS_MS[Math.min(previousRetryCount, RETRY_DELAYS_MS.length - 1)],
    })
    .configureLogging(LogLevel.None)
    .build();
}

export interface SubscribeOptions {
  connect?: () => RealtimeConnection;
  retryDelaysMs?: readonly number[];
}

function isDeckEvent(value: unknown): value is DeckEvent {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as { type?: unknown }).type === 'string' &&
    typeof (value as { deckId?: unknown }).deckId === 'string'
  );
}

// Connects to the hub, joins the deck and calls onEvent for each of its events. A connection that cannot be made is
// retried with back-off; after SignalR reconnects a dropped connection the deck is joined again (the server forgets
// the groups of a dead connection). Events missed while offline are not replayed, so a screen that must not miss a
// change re-reads after a reconnect. The returned function stops everything: no more events, no more retries, the
// connection closed.
export function subscribeToDeck(
  deckId: string,
  onEvent: (event: DeckEvent) => void,
  { connect = createDeckConnection, retryDelaysMs = RETRY_DELAYS_MS }: SubscribeOptions = {},
): () => void {
  const connection = connect();
  const wanted = deckId.toLowerCase();
  let stopped = false;
  let retry: ReturnType<typeof setTimeout> | undefined;

  const join = async () => {
    try {
      await connection.invoke('JoinDeck', deckId);
    } catch {
      // A deck the caller can't open: the screen shows its own error from the REST call, nothing to retry here.
    }
  };

  connection.on(EVENT_METHOD, (event: unknown) => {
    if (!stopped && isDeckEvent(event) && event.deckId.toLowerCase() === wanted) onEvent(event);
  });
  connection.onreconnected(() => {
    if (!stopped) void join();
  });

  const open = async (attempt: number) => {
    try {
      await connection.start();
    } catch {
      if (stopped) return;
      const delay = retryDelaysMs[Math.min(attempt, retryDelaysMs.length - 1)];
      retry = setTimeout(() => void open(attempt + 1), delay);
      return;
    }
    if (stopped) return;
    await join();
  };
  void open(0);

  return () => {
    stopped = true;
    clearTimeout(retry);
    void connection.stop();
  };
}

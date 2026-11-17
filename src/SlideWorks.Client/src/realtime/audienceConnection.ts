import {
  HttpTransportType,
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { DECK_HUB_PATH, RETRY_DELAYS_MS } from './useDeckConnection';

// The guest page's own hub connection (SW-5 J5, D7). Guests are not signed in and may sit behind a network that blocks
// WebSockets, so it offers all three transports: SignalR tries them in the order of the server's negotiate answer, which
// lists WebSockets, then Server-Sent Events, then long polling. The signed-in pages use useDeckConnection instead.
export const AUDIENCE_TRANSPORTS =
  HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;

// Seconds-scale back-off for a dropped connection; the last delay repeats. The same delays SW-1's shared connection uses.
export function audienceRetryDelayMs(previousRetryCount: number): number {
  return RETRY_DELAYS_MS[Math.min(previousRetryCount, RETRY_DELAYS_MS.length - 1)];
}

// The browser sends the sw_guest cookie on the negotiate request and on every later request of the same origin, so there
// is no token to pass. JoinAudience (J4) decides what a guest may see.
export function createAudienceConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(DECK_HUB_PATH, { transport: AUDIENCE_TRANSPORTS })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
        audienceRetryDelayMs(previousRetryCount),
    })
    .configureLogging(LogLevel.None)
    .build();
}

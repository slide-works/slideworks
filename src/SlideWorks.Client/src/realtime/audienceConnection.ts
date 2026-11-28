import { HttpTransportType, type HubConnection } from '@microsoft/signalr';
import { createHubConnection } from '@slideworks/core/realtime/hub';
import { RETRY_DELAYS_MS } from './useDeckConnection';

// The guest page's own hub connection (SW-5 J5, D7). Guests are not signed in and may sit behind a network that blocks
// WebSockets, so it offers all three transports: SignalR tries them in the order of the server's negotiate answer, which
// lists WebSockets, then Server-Sent Events, then long polling. The signed-in pages use useDeckConnection instead.
export const AUDIENCE_TRANSPORTS =
  HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;

// Seconds-scale back-off for a dropped connection, the same delays SW-1's shared connection uses. After one pass through
// them it returns null, so SignalR stops retrying and closes the connection, which is what lets the guest page show
// offline.
export function audienceRetryDelayMs(previousRetryCount: number): number | null {
  return RETRY_DELAYS_MS[previousRetryCount] ?? null;
}

// The browser sends the sw_guest cookie on the negotiate request and on every later request of the same origin, so there
// is no token to pass. JoinAudience (J4) decides what a guest may see.
export function createAudienceConnection(): HubConnection {
  return createHubConnection({
    auth: { kind: 'ambient' },
    transport: AUDIENCE_TRANSPORTS,
    nextRetryDelayMs: audienceRetryDelayMs,
  });
}

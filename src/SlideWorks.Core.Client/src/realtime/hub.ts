import {
  HubConnectionBuilder,
  LogLevel,
  type HttpTransportType,
  type HubConnection,
  type IHttpConnectionOptions,
} from '@microsoft/signalr';
import { platform } from '../platform';

// The server side is src/SlideWorks.WebApi/Realtime/DeckHub.cs.
export const DECK_HUB_PATH = '/hubs/deck';

// What proves who opens a hub connection (contract K3).
export type HubAuth =
  // The web: the browser sends the cookie on the same-origin negotiate request and WebSocket upgrade.
  | { kind: 'ambient' }
  // The app, signed in, to apiOrigin(). SignalR sends the secret as Authorization on negotiate and as the access_token
  // query on the WebSocket, which can't carry a header.
  | { kind: 'bearer'; secret: () => string | null }
  // The app, as a guest of that origin: the guest id rides as the guest query, which the hub reads like the header.
  | { kind: 'guest'; origin: string; guestId: () => string | null };

export type HubConnectionOptions = {
  auth: HubAuth;
  transport: HttpTransportType;
  // The delay before the next reconnect attempt, or null to stop retrying and close.
  nextRetryDelayMs: (previousRetryCount: number) => number | null;
};

export function createHubConnection(options: HubConnectionOptions): HubConnection {
  const { url, connection } = target(options.auth, options.transport);
  return new HubConnectionBuilder()
    .withUrl(url, connection)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
        options.nextRetryDelayMs(previousRetryCount),
    })
    .configureLogging(LogLevel.None)
    .build();
}

function target(
  auth: HubAuth,
  transport: HttpTransportType,
): { url: string; connection: IHttpConnectionOptions } {
  switch (auth.kind) {
    case 'ambient':
      return { url: DECK_HUB_PATH, connection: { transport } };
    case 'bearer':
      return {
        url: platform().apiOrigin() + DECK_HUB_PATH,
        // SignalR adds no Authorization header for an empty token.
        connection: { transport, accessTokenFactory: () => auth.secret() ?? '' },
      };
    case 'guest': {
      const id = auth.guestId();
      const query = id === null ? '' : `?guest=${encodeURIComponent(id)}`;
      return { url: auth.origin + DECK_HUB_PATH + query, connection: { transport } };
    }
  }
}

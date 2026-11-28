import { HttpTransportType, HubConnectionBuilder, type HubConnection } from '@microsoft/signalr';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { configureTestPlatform } from '../test-support/testPlatform';
import { DECK_HUB_PATH, createHubConnection } from './hub';

type ConnectionOptions = { transport: HttpTransportType; accessTokenFactory?: () => string };

let withUrl: ReturnType<typeof spyOnWithUrl>;

function spyOnWithUrl() {
  return vi.spyOn(HubConnectionBuilder.prototype, 'withUrl');
}

beforeEach(() => {
  withUrl = spyOnWithUrl();
  // Building a real connection would need a reachable server; the URL and options are what the tests check.
  vi.spyOn(HubConnectionBuilder.prototype, 'build').mockReturnValue({} as HubConnection);
});
afterEach(() => vi.restoreAllMocks());

const urlAndOptions = () => withUrl.mock.calls[0] as unknown as [string, ConnectionOptions];

describe('hub', () => {
  it('ambient connects to the same-origin deck hub with no token', () => {
    createHubConnection({
      auth: { kind: 'ambient' },
      transport: HttpTransportType.WebSockets,
      nextRetryDelayMs: () => null,
    });

    expect(withUrl).toHaveBeenCalledExactlyOnceWith(DECK_HUB_PATH, {
      transport: HttpTransportType.WebSockets,
    });
  });

  it('bearer uses apiOrigin and accessTokenFactory', async () => {
    configureTestPlatform();
    let secret: string | null = 'swd_first';

    createHubConnection({
      auth: { kind: 'bearer', secret: () => secret },
      transport: HttpTransportType.WebSockets,
      nextRetryDelayMs: () => null,
    });

    const [url, options] = urlAndOptions();
    expect(url).toBe('https://signed-in.example/hubs/deck');
    expect(options.transport).toBe(HttpTransportType.WebSockets);
    expect(await options.accessTokenFactory?.()).toBe('swd_first');
    secret = null;
    expect(await options.accessTokenFactory?.()).toBe('');
  });

  it('guest adds guest query for its origin', () => {
    configureTestPlatform();

    createHubConnection({
      auth: {
        kind: 'guest',
        origin: 'https://talk.example',
        guestId: () => '0123456789abcdef0123456789abcdef',
      },
      transport: HttpTransportType.LongPolling,
      nextRetryDelayMs: () => null,
    });

    const [url, options] = urlAndOptions();
    expect(url).toBe('https://talk.example/hubs/deck?guest=0123456789abcdef0123456789abcdef');
    expect(options).toEqual({ transport: HttpTransportType.LongPolling });
  });

  it('guest without an id connects with no guest query', () => {
    createHubConnection({
      auth: { kind: 'guest', origin: 'https://talk.example', guestId: () => null },
      transport: HttpTransportType.WebSockets,
      nextRetryDelayMs: () => null,
    });

    expect(urlAndOptions()[0]).toBe('https://talk.example/hubs/deck');
  });

  it('reconnects with the given delays', () => {
    const withAutomaticReconnect = vi.spyOn(
      HubConnectionBuilder.prototype,
      'withAutomaticReconnect',
    );

    createHubConnection({
      auth: { kind: 'ambient' },
      transport: HttpTransportType.WebSockets,
      nextRetryDelayMs: (count) => (count < 2 ? count * 1000 : null),
    });

    const policy = withAutomaticReconnect.mock.calls[0][0] as {
      nextRetryDelayInMilliseconds(context: { previousRetryCount: number }): number | null;
    };
    const seen = [0, 1, 2].map((previousRetryCount) =>
      policy.nextRetryDelayInMilliseconds({ previousRetryCount }),
    );
    expect(seen).toEqual([0, 1000, null]);
  });
});

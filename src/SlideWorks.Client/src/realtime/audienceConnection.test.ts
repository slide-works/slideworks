import { HttpTransportType, HubConnectionBuilder, type HubConnection } from '@microsoft/signalr';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  AUDIENCE_TRANSPORTS,
  audienceRetryDelayMs,
  createAudienceConnection,
} from './audienceConnection';
import { RETRY_DELAYS_MS } from './useDeckConnection';

afterEach(() => vi.restoreAllMocks());

describe('AUDIENCE_TRANSPORTS', () => {
  it('offers WebSockets, Server-Sent Events and long polling', () => {
    expect(AUDIENCE_TRANSPORTS & HttpTransportType.WebSockets).toBeTruthy();
    expect(AUDIENCE_TRANSPORTS & HttpTransportType.ServerSentEvents).toBeTruthy();
    expect(AUDIENCE_TRANSPORTS & HttpTransportType.LongPolling).toBeTruthy();
  });
});

describe('audienceRetryDelayMs', () => {
  it("uses SW-1's delays in order", () => {
    const delays = RETRY_DELAYS_MS.map((_, attempt) => audienceRetryDelayMs(attempt));
    expect(delays).toEqual([...RETRY_DELAYS_MS]);
  });

  it('returns the last delay for the last retry', () => {
    expect(audienceRetryDelayMs(RETRY_DELAYS_MS.length - 1)).toBe(RETRY_DELAYS_MS.at(-1));
  });

  it('stops once every delay has been used', () => {
    expect(audienceRetryDelayMs(RETRY_DELAYS_MS.length)).toBeNull();
    expect(audienceRetryDelayMs(RETRY_DELAYS_MS.length + 7)).toBeNull();
  });
});

describe('createAudienceConnection', () => {
  it('connects to the deck hub with all three transports and the shared retry delays', () => {
    const withUrl = vi.spyOn(HubConnectionBuilder.prototype, 'withUrl');
    const withAutomaticReconnect = vi.spyOn(
      HubConnectionBuilder.prototype,
      'withAutomaticReconnect',
    );

    // Building a real connection resolves the relative URL against a browser location, which this environment lacks.
    vi.spyOn(HubConnectionBuilder.prototype, 'build').mockReturnValue({} as HubConnection);

    createAudienceConnection();

    expect(withUrl).toHaveBeenCalledExactlyOnceWith('/hubs/deck', {
      transport: AUDIENCE_TRANSPORTS,
    });
    const policy = withAutomaticReconnect.mock.calls[0][0] as {
      nextRetryDelayInMilliseconds(context: { previousRetryCount: number }): number | null;
    };
    const seen = RETRY_DELAYS_MS.map((_, previousRetryCount) =>
      policy.nextRetryDelayInMilliseconds({ previousRetryCount }),
    );
    expect(seen).toEqual([...RETRY_DELAYS_MS]);
    expect(
      policy.nextRetryDelayInMilliseconds({ previousRetryCount: RETRY_DELAYS_MS.length }),
    ).toBeNull();
  });
});

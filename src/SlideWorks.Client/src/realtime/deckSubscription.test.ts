import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DeckEvent } from './deckEvents';
import { subscribeToDeck, type RealtimeConnection } from './deckSubscription';

const deck = '0199a000-0000-7000-8000-000000000012';
const changed: DeckEvent = { type: 'deckChanged', deckId: deck };

// A hub connection that does what the test tells it to.
function fakeConnection(startResults: ('ok' | 'fail')[] = ['ok']) {
  const handlers: Record<string, (event: unknown) => void> = {};
  let onReconnected: () => void = () => undefined;
  const connection = {
    start: vi.fn(async () => {
      if (startResults.shift() === 'fail') throw new Error('offline');
    }),
    stop: vi.fn(async () => undefined),
    invoke: vi.fn(async () => undefined),
    on: vi.fn((method: string, handler: (event: unknown) => void) => {
      handlers[method] = handler;
    }),
    onreconnected: vi.fn((handler: () => void) => {
      onReconnected = handler;
    }),
  };
  return {
    connection: connection as unknown as RealtimeConnection,
    spies: connection,
    deliver: (event: unknown) => handlers['deckEvent']?.(event),
    reconnect: () => onReconnected(),
  };
}

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe('subscribeToDeck', () => {
  it('joins the deck once connected and passes its events on', async () => {
    const hub = fakeConnection();
    const received: DeckEvent[] = [];

    subscribeToDeck(deck, (event) => received.push(event), { connect: () => hub.connection });
    await vi.advanceTimersByTimeAsync(0);
    hub.deliver(changed);

    expect(hub.spies.invoke).toHaveBeenCalledExactlyOnceWith('JoinDeck', deck);
    expect(received).toEqual([changed]);
  });

  it('matches the deck id whatever its case', async () => {
    const hub = fakeConnection();
    const received: DeckEvent[] = [];
    subscribeToDeck(deck.toUpperCase(), (event) => received.push(event), {
      connect: () => hub.connection,
    });
    await vi.advanceTimersByTimeAsync(0);

    hub.deliver(changed);

    expect(received).toEqual([changed]);
  });

  it('ignores events of another deck and anything that is not an event', async () => {
    const hub = fakeConnection();
    const received: DeckEvent[] = [];
    subscribeToDeck(deck, (event) => received.push(event), { connect: () => hub.connection });
    await vi.advanceTimersByTimeAsync(0);

    hub.deliver({ ...changed, deckId: '0199a000-0000-7000-8000-000000000099' });
    hub.deliver('hello');
    hub.deliver(null);

    expect(received).toEqual([]);
  });

  it('joins the deck again after SignalR reconnects', async () => {
    const hub = fakeConnection();
    subscribeToDeck(deck, () => undefined, { connect: () => hub.connection });
    await vi.advanceTimersByTimeAsync(0);

    hub.reconnect();

    expect(hub.spies.invoke).toHaveBeenCalledTimes(2);
    expect(hub.spies.invoke).toHaveBeenLastCalledWith('JoinDeck', deck);
  });

  it('retries a first connection that fails, with back-off, and joins when it succeeds', async () => {
    const hub = fakeConnection(['fail', 'fail', 'ok']);

    subscribeToDeck(deck, () => undefined, {
      connect: () => hub.connection,
      retryDelaysMs: [1000, 5000],
    });
    await vi.advanceTimersByTimeAsync(0);
    expect(hub.spies.start).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(999);
    expect(hub.spies.start).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(hub.spies.start).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(5000);

    expect(hub.spies.start).toHaveBeenCalledTimes(3);
    expect(hub.spies.invoke).toHaveBeenCalledExactlyOnceWith('JoinDeck', deck);
  });

  it('unsubscribing closes the connection, drops later events and cancels a pending retry', async () => {
    const failing = fakeConnection(['fail', 'ok']);
    const received: DeckEvent[] = [];
    const stop = subscribeToDeck(deck, (event) => received.push(event), {
      connect: () => failing.connection,
      retryDelaysMs: [1000],
    });
    await vi.advanceTimersByTimeAsync(0);

    stop();
    await vi.advanceTimersByTimeAsync(5000);
    failing.deliver(changed);

    expect(failing.spies.start).toHaveBeenCalledTimes(1);
    expect(failing.spies.stop).toHaveBeenCalledTimes(1);
    expect(failing.spies.invoke).not.toHaveBeenCalled();
    expect(received).toEqual([]);
  });

  it('does not join a connection that was unsubscribed while it was starting', async () => {
    const hub = fakeConnection();
    const stop = subscribeToDeck(deck, () => undefined, { connect: () => hub.connection });

    stop();
    await vi.advanceTimersByTimeAsync(0);

    expect(hub.spies.invoke).not.toHaveBeenCalled();
  });

  it('keeps going when joining fails', async () => {
    const hub = fakeConnection();
    hub.spies.invoke.mockRejectedValueOnce(new Error('HubException: not_found'));
    const received: DeckEvent[] = [];

    subscribeToDeck(deck, (event) => received.push(event), { connect: () => hub.connection });
    await vi.advanceTimersByTimeAsync(0);
    hub.deliver(changed);

    expect(received).toEqual([changed]);
  });
});

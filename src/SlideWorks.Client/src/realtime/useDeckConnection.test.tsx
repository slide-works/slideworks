import { act, StrictMode, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  acquireDeckConnection,
  useDeckConnection,
  type RealtimeConnection,
} from './useDeckConnection';

// A hub connection that does what the test tells it to.
const fakes = vi.hoisted(() => {
  function fakeConnection(startResults: ('ok' | 'fail')[] = ['ok']) {
    const events = {
      reconnecting: (): void => undefined,
      reconnected: (): void => undefined,
      close: (): void => undefined,
    };
    const spies = {
      start: vi.fn(async () => {
        if (startResults.shift() === 'fail') throw new Error('offline');
      }),
      stop: vi.fn(async () => undefined),
      invoke: vi.fn(async () => undefined),
      on: vi.fn(),
      onreconnecting: vi.fn((handler: () => void) => (events.reconnecting = handler)),
      onreconnected: vi.fn((handler: () => void) => (events.reconnected = handler)),
      onclose: vi.fn((handler: () => void) => (events.close = handler)),
    };
    return {
      spies,
      reconnecting: () => events.reconnecting(),
      reconnected: () => events.reconnected(),
      close: () => events.close(),
    };
  }
  // Every connection the real createDeckConnection builds, through the mocked HubConnectionBuilder below.
  const built: ReturnType<typeof fakeConnection>[] = [];
  return { fakeConnection, built };
});

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      const fake = fakes.fakeConnection();
      fakes.built.push(fake);
      return fake.spies;
    }
  }
  return { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } };
});

const asConnection = (fake: ReturnType<typeof fakes.fakeConnection>) =>
  fake.spies as unknown as RealtimeConnection;

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe('acquireDeckConnection', () => {
  it('opens one connection for two leases with the same connect, and keeps it open until the last release', async () => {
    const fake = fakes.fakeConnection();
    const connect = vi.fn(() => asConnection(fake));

    const first = acquireDeckConnection({ connect });
    const second = acquireDeckConnection({ connect });
    await vi.advanceTimersByTimeAsync(0);

    expect(connect).toHaveBeenCalledOnce();
    expect(fake.spies.start).toHaveBeenCalledOnce();
    first.release();
    expect(fake.spies.stop).not.toHaveBeenCalled();
    second.release();
    expect(fake.spies.stop).toHaveBeenCalledOnce();
  });

  it('cancels a pending first-start retry on the last release', async () => {
    const fake = fakes.fakeConnection(['fail', 'ok']);
    const lease = acquireDeckConnection({
      connect: () => asConnection(fake),
      retryDelaysMs: [1000],
    });
    await vi.advanceTimersByTimeAsync(0);

    lease.release();
    await vi.advanceTimersByTimeAsync(5000);

    expect(fake.spies.start).toHaveBeenCalledOnce();
    expect(fake.spies.stop).toHaveBeenCalledOnce();
  });

  it('opens a new connection for an acquire after the last release', async () => {
    const fakesMade = [fakes.fakeConnection(), fakes.fakeConnection()];
    let next = 0;
    const connect = () => asConnection(fakesMade[next++]);

    acquireDeckConnection({ connect }).release();
    const again = acquireDeckConnection({ connect });
    await vi.advanceTimersByTimeAsync(0);

    expect(next).toBe(2);
    expect(fakesMade[1].spies.start).toHaveBeenCalledOnce();
    again.release();
  });

  it('calls onUp straight away when the connection is already up, and after a reconnect', async () => {
    const fake = fakes.fakeConnection();
    const connect = () => asConnection(fake);
    const first = acquireDeckConnection({ connect });
    await vi.advanceTimersByTimeAsync(0);

    const second = acquireDeckConnection({ connect });
    const onUp = vi.fn();
    second.onUp(onUp);
    expect(onUp).toHaveBeenCalledOnce();
    fake.reconnecting();
    fake.reconnected();
    expect(onUp).toHaveBeenCalledTimes(2);

    first.release();
    second.release();
  });

  it('calls onDown once when an up connection starts reconnecting and then closes', async () => {
    const fake = fakes.fakeConnection();
    const lease = acquireDeckConnection({ connect: () => asConnection(fake) });
    const onDown = vi.fn();
    lease.onDown(onDown);
    await vi.advanceTimersByTimeAsync(0);

    fake.reconnecting();
    fake.close();

    expect(onDown).toHaveBeenCalledOnce();
    lease.release();
  });

  it('says to leave a group only for its last member while another lease keeps the connection open', () => {
    const fake = fakes.fakeConnection();
    const connect = () => asConnection(fake);
    const page = acquireDeckConnection({ connect });
    const a = acquireDeckConnection({ connect });
    const b = acquireDeckConnection({ connect });
    const leaveA = a.member('deck:one');
    const leaveB = b.member('deck:one');

    expect(leaveA()).toBe(false);
    expect(leaveA()).toBe(false);
    expect(leaveB()).toBe(true);

    const alone = acquireDeckConnection({ connect: () => asConnection(fakes.fakeConnection()) });
    expect(alone.member('deck:one')()).toBe(false);
    for (const lease of [page, a, b, alone]) lease.release();
  });
});

describe('useDeckConnection', () => {
  let container: HTMLDivElement;
  let root: Root;

  beforeEach(() => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    fakes.built.length = 0;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  // Listens for the connection coming up, which takes the component's lease.
  function User() {
    const lease = useDeckConnection();
    useEffect(() => lease.onUp(() => undefined), [lease]);
    return null;
  }

  const open = () => fakes.built.filter((fake) => fake.spies.stop.mock.calls.length === 0);

  it('shares one connection between components, and closes it when the last one unmounts, under StrictMode', async () => {
    const render = (users: number) =>
      act(() =>
        root.render(
          <StrictMode>
            {Array.from({ length: users }, (_, i) => (
              <User key={i} />
            ))}
          </StrictMode>,
        ),
      );

    await render(2);
    await vi.advanceTimersByTimeAsync(0);
    expect(open()).toHaveLength(1);
    expect(open()[0].spies.start).toHaveBeenCalledOnce();

    await render(1);
    expect(open()).toHaveLength(1);

    await render(0);
    expect(open()).toHaveLength(0);
  });
});

import {
  HttpTransportType,
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { useEffect, useState } from 'react';

// The server side is src/SlideWorks.WebApi/Realtime/DeckHub.cs.
export const DECK_HUB_PATH = '/hubs/deck';

// The slice of HubConnection the shared connection uses, so a test can pass a fake.
export type RealtimeConnection = Pick<
  HubConnection,
  'start' | 'stop' | 'invoke' | 'on' | 'onreconnecting' | 'onreconnected' | 'onclose'
>;

// Seconds-scale back-off, used both for the first connect (SignalR does not retry that) and, through
// withAutomaticReconnect, for a dropped connection. The last delay repeats.
export const RETRY_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000];

// WebSockets only: the hub offers no other transport. The browser sends the session cookie on the negotiate request and
// on the same-origin WebSocket upgrade, so there is no token to pass. Anyone may connect; each Join call decides.
export function createDeckConnection(): RealtimeConnection {
  return new HubConnectionBuilder()
    .withUrl(DECK_HUB_PATH, { transport: HttpTransportType.WebSockets })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
        RETRY_DELAYS_MS[Math.min(previousRetryCount, RETRY_DELAYS_MS.length - 1)],
    })
    .configureLogging(LogLevel.None)
    .build();
}

// One lease on the page's shared deck hub connection. Everything that talks to DeckHub in a page (deck events through
// subscribeToDeck, a presentation through usePresentation) holds a lease on the same connection, so a page opens one
// WebSocket however many of them it uses.
export interface DeckConnectionLease {
  invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T>;
  // Listens for a server → client message. Returns the function that stops listening.
  on(method: string, handler: (...args: unknown[]) => void): () => void;
  // Called each time the connection is up: after the first start, after every reconnect, and straight away when it is
  // already up. The server forgets a dead connection's groups, so this is where a caller joins.
  onUp(listener: () => void): () => void;
  // Called each time an up connection goes down (SignalR is reconnecting, or it closed).
  onDown(listener: () => void): () => void;
  // Counts this lease as a member of a hub group (such as "deck:{id}") on the shared connection, so two parts of a page
  // in the same group don't leave it under each other. Returns the function that takes the membership back, which says
  // whether the caller should now send the group's Leave call: true when this was the group's last member and another
  // lease keeps the connection open. When the connection closes instead, the server drops its groups by itself.
  member(group: string): () => boolean;
  // Gives the lease back. The connection closes when the last lease is given back.
  release(): void;
}

export interface DeckConnectionOptions {
  // For tests: the connection to use, and the back-off of the first connect. Leases with the same connect share one
  // connection, so every caller that leaves it out shares the page's real one.
  connect?: () => RealtimeConnection;
  retryDelaysMs?: readonly number[];
}

type Shared = {
  connection: RealtimeConnection;
  leases: number;
  up: boolean;
  stopped: boolean;
  retry: ReturnType<typeof setTimeout> | undefined;
  upListeners: Set<() => void>;
  downListeners: Set<() => void>;
  handlers: Map<string, Set<(...args: unknown[]) => void>>;
  members: Map<string, number>;
};

const shared = new Map<() => RealtimeConnection, Shared>();

function open(connect: () => RealtimeConnection, retryDelaysMs: readonly number[]): Shared {
  const hub: Shared = {
    connection: connect(),
    leases: 0,
    up: false,
    stopped: false,
    retry: undefined,
    upListeners: new Set(),
    downListeners: new Set(),
    handlers: new Map(),
    members: new Map(),
  };
  const goUp = () => {
    if (hub.stopped) return;
    hub.up = true;
    for (const listener of [...hub.upListeners]) listener();
  };
  const goDown = () => {
    if (hub.stopped || !hub.up) return;
    hub.up = false;
    for (const listener of [...hub.downListeners]) listener();
  };
  hub.connection.onreconnecting(goDown);
  hub.connection.onreconnected(goUp);
  hub.connection.onclose(goDown);

  // SignalR does not retry a first connect that fails, so this does, with back-off. A dropped connection is retried
  // by withAutomaticReconnect (createDeckConnection).
  const start = async (attempt: number) => {
    try {
      await hub.connection.start();
    } catch {
      if (hub.stopped) return;
      const delay = retryDelaysMs[Math.min(attempt, retryDelaysMs.length - 1)];
      hub.retry = setTimeout(() => void start(attempt + 1), delay);
      return;
    }
    goUp();
  };
  void start(0);
  return hub;
}

// Takes a lease on the shared connection, opening it if this is the first. Not a hook: subscribeToDeck uses it, and
// useDeckConnection wraps it for components.
export function acquireDeckConnection({
  connect = createDeckConnection,
  retryDelaysMs = RETRY_DELAYS_MS,
}: DeckConnectionOptions = {}): DeckConnectionLease {
  let hub = shared.get(connect);
  if (!hub) {
    hub = open(connect, retryDelaysMs);
    shared.set(connect, hub);
  }
  const current = hub;
  current.leases += 1;

  let released = false;
  const mine = new Set<() => void>();
  const track = (remove: () => void) => {
    const once = () => {
      remove();
      mine.delete(once);
    };
    mine.add(once);
    return once;
  };

  return {
    invoke<T>(method: string, ...args: unknown[]) {
      return current.connection.invoke<T>(method, ...args);
    },
    on(method, handler) {
      let handlers = current.handlers.get(method);
      if (!handlers) {
        const set = new Set<(...args: unknown[]) => void>();
        handlers = set;
        current.handlers.set(method, set);
        current.connection.on(method, (...args: unknown[]) => {
          for (const each of [...set]) each(...args);
        });
      }
      const set = handlers;
      const wrapped = (...args: unknown[]) => {
        if (!released) handler(...args);
      };
      set.add(wrapped);
      return track(() => set.delete(wrapped));
    },
    onUp(listener) {
      const wrapped = () => {
        if (!released) listener();
      };
      current.upListeners.add(wrapped);
      if (current.up) listener();
      return track(() => current.upListeners.delete(wrapped));
    },
    onDown(listener) {
      const wrapped = () => {
        if (!released) listener();
      };
      current.downListeners.add(wrapped);
      return track(() => current.downListeners.delete(wrapped));
    },
    member(group) {
      current.members.set(group, (current.members.get(group) ?? 0) + 1);
      let counted = true;
      const leave = () => {
        if (!counted) return false;
        counted = false;
        const left = (current.members.get(group) ?? 1) - 1;
        if (left > 0) current.members.set(group, left);
        else current.members.delete(group);
        return left === 0 && !released && current.leases > 1;
      };
      const tracked = track(() => void leave());
      return () => {
        const last = leave();
        tracked();
        return last;
      };
    },
    release() {
      if (released) return;
      released = true;
      for (const remove of [...mine]) remove();
      current.leases -= 1;
      if (current.leases > 0) return;
      current.stopped = true;
      clearTimeout(current.retry);
      if (shared.get(connect) === current) shared.delete(connect);
      void current.connection.stop();
    },
  };
}

// A lease taken on first use, so a component can hold one from its first render without a side effect there. Listening
// (on, onUp, onDown, member) takes the lease; invoke before that rejects, and release gives it back, after which the next use
// takes a new one.
function lazyLease(): DeckConnectionLease {
  let lease: DeckConnectionLease | undefined;
  const take = () => (lease ??= acquireDeckConnection());
  return {
    invoke<T>(method: string, ...args: unknown[]) {
      return lease
        ? lease.invoke<T>(method, ...args)
        : Promise.reject(new Error('The deck hub connection is not open.'));
    },
    on: (method, handler) => take().on(method, handler),
    onUp: (listener) => take().onUp(listener),
    onDown: (listener) => take().onDown(listener),
    member: (group) => take().member(group),
    release() {
      lease?.release();
      lease = undefined;
    },
  };
}

// The page's one deck hub connection, for as long as the calling component is mounted. The returned lease is the same
// object on every render. Every component that uses it (and every subscribeToDeck) shares one WebSocket.
export function useDeckConnection(): DeckConnectionLease {
  const [lease] = useState(lazyLease);
  useEffect(() => () => lease.release(), [lease]);
  return lease;
}

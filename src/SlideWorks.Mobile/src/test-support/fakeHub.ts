import type { HubAuth, HubConnectionOptions } from '@slideworks/core/realtime/hub';

// fakeHub() stands in for the core's createHubConnection (jest.setup.ts routes it here): the next connection a screen
// builds is this fake. It records the HubAuth it was built with and every invoke; a test answers invokes with
// respond(), pushes server events with emit(), and drops and restores the connection.

type Handler = (...args: unknown[]) => void;
export type HubInvoke = { method: string; args: unknown[] };
export type HubState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';

export type FakeHub = {
  // The auth of the last connection built, or null before one is.
  readonly auth: HubAuth | null;
  readonly options: HubConnectionOptions | null;
  readonly builds: number;
  readonly invokes: HubInvoke[];
  readonly state: HubState;
  respond(method: string, value: unknown | ((...args: unknown[]) => unknown)): void;
  emit(event: string, ...args: unknown[]): void;
  drop(error?: Error): void;
  restore(): void;
};

let current: (FakeHub & { build(options: HubConnectionOptions): unknown }) | null = null;

export function resetFakeHub(): void {
  current = null;
}

export function fakeHubFactory(options: HubConnectionOptions): unknown {
  if (!current)
    throw new Error('A screen built a hub connection: call fakeHub() in the test first.');
  return current.build(options);
}

export function fakeHub(): FakeHub {
  const handlers = new Map<string, Set<Handler>>();
  const lifecycle = {
    reconnecting: [] as Handler[],
    reconnected: [] as Handler[],
    close: [] as Handler[],
  };
  const responses = new Map<string, unknown>();
  const invokes: HubInvoke[] = [];
  let state: HubState = 'Disconnected';
  let options: HubConnectionOptions | null = null;
  let builds = 0;

  const connection = {
    get state() {
      return state;
    },
    async start() {
      state = 'Connected';
    },
    async stop() {
      state = 'Disconnected';
      for (const handler of lifecycle.close) handler(undefined);
    },
    on(event: string, handler: Handler) {
      if (!handlers.has(event)) handlers.set(event, new Set());
      handlers.get(event)?.add(handler);
    },
    off(event: string, handler?: Handler) {
      if (handler) handlers.get(event)?.delete(handler);
      else handlers.delete(event);
    },
    async invoke(method: string, ...args: unknown[]) {
      invokes.push({ method, args });
      if (!responses.has(method)) throw new Error(`fakeHub: no response for ${method}`);
      const value = responses.get(method);
      return typeof value === 'function' ? value(...args) : value;
    },
    async send(method: string, ...args: unknown[]) {
      invokes.push({ method, args });
    },
    onreconnecting: (handler: Handler) => void lifecycle.reconnecting.push(handler),
    onreconnected: (handler: Handler) => void lifecycle.reconnected.push(handler),
    onclose: (handler: Handler) => void lifecycle.close.push(handler),
  };

  const hub = {
    get auth() {
      return options?.auth ?? null;
    },
    get options() {
      return options;
    },
    get builds() {
      return builds;
    },
    invokes,
    get state() {
      return state;
    },
    respond: (method: string, value: unknown) => void responses.set(method, value),
    emit(event: string, ...args: unknown[]) {
      for (const handler of handlers.get(event) ?? []) handler(...args);
    },
    drop(error = new Error('Connection lost')) {
      state = 'Reconnecting';
      for (const handler of lifecycle.reconnecting) handler(error);
    },
    restore() {
      state = 'Connected';
      for (const handler of lifecycle.reconnected) handler('fake-connection-id');
    },
    build(built: HubConnectionOptions) {
      options = built;
      builds += 1;
      return connection;
    },
  };
  current = hub;
  return hub;
}

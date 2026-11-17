import { createServer } from 'node:http';
import type { AddressInfo } from 'node:net';
import type { Page } from 'playwright/test';
import type { AudienceEvent, AudienceState } from '../guest/audienceEvents';
import type { PresentationState, PresentationUpdate, TimerCommand } from '../presentation/types';
import { DECK_NOT_FOUND } from '../realtime/hubErrors';

// SignalR's JSON hub protocol ends every message with a record separator.
const RS = '\u001e';

// The text the real hub's error completion carries when a method throws HubException(message): ASP.NET Core always
// appends "HubException: <message>", with or without detailed errors.
function hubError(target: string, message: string): string {
  return `An unexpected error occurred invoking '${target}' on the server. HubException: ${message}`;
}

// What the real hub answers for a method it doesn't have.
function missingMethod(target: string): string {
  return `Failed to invoke '${target}' due to an error on the server. HubException: Method does not exist.`;
}

export interface StubPresentationOptions {
  // The state JoinPresentation answers with, as it is when the join arrives. UpdatePresentation, SetTimer and
  // pushState change it.
  state: PresentationState;
  // The clockMinutes JoinPresentation answers with. 872 (14:32) when left out.
  clockMinutes?: number;
  // Refuse every JoinPresentation, as the hub does for anyone but the deck's owner.
  refuse?: boolean;
  // Leave each JoinPresentation unanswered until releasePresentationJoins(), as a slow join would be.
  holdJoin?: boolean;
}

// The hub's transports (SW-5 D7), in the order the real server's negotiate answer lists them.
export type StubTransport = 'WebSockets' | 'ServerSentEvents' | 'LongPolling';

export interface StubAudienceOptions {
  // The state JoinAudience answers with. A later pushAudienceEvent of an audienceState changes it for later joins.
  state: AudienceState;
  // Refuse every JoinAudience with this HubException message, as the hub does for an unknown code ("no_session"), a
  // guest with no sw_guest cookie ("no_guest") or one that missed too often ("too_many_attempts").
  refuse?: 'no_session' | 'no_guest' | 'too_many_attempts';
}

export interface StubDeckHubOptions {
  // Leave each JoinDeck unanswered until releaseJoins(), as a slow join would be.
  holdJoins?: boolean;
  // Play the presentation calls (contract P2) too. Without it, JoinPresentation, UpdatePresentation and SetTimer are
  // answered as a method the hub doesn't have.
  presentation?: StubPresentationOptions;
  // Play the audience calls (contract J4) too. Without it, JoinAudience, LeaveAudience and WatchAudience are answered as
  // a method the hub doesn't have.
  audience?: StubAudienceOptions;
  // The transports negotiate offers. All three when left out, as the real hub does, so a client that can use any of them
  // gets WebSockets. Give one to force a fallback.
  transports?: readonly StubTransport[];
}

// The arguments of one JoinPresentation, in the order the hub takes them (P2).
export type StubPresentationJoin = {
  deckId: string;
  clientId: string;
  role: string;
  label: string;
  timeZone: string;
  resume: unknown;
};

type HubMessage = {
  protocol?: string;
  type?: number;
  invocationId?: string;
  target?: string;
  arguments?: unknown[];
};

// How a stubbed hub method answers one invocation: with a result (or none, for a Task method), or with the error the
// real hub sends when the method throws HubException(message).
type Reply = { result(value?: unknown): void; error(message: string): void };

// One hub method. record runs for every invocation, failed ones included, so a test sees each call the page made;
// answer runs unless fail() has armed a failure for the method.
type HubMethod = { record?(args: unknown[]): void; answer(args: unknown[], reply: Reply): void };

// Somewhere to send hub frames: a WebSocket, a held long poll or an open event stream.
type Sink = { send(text: string): void };

// Component tests only: plays the deck hub (/hubs/deck) at the network boundary, deck events and presentations both.
// It answers negotiate, the handshake and JoinDeck, records the deck ids each JoinDeck asked for, and pushes deckEvents
// to the client the way the server does. With the presentation option it also answers JoinPresentation (or refuses
// it), records each UpdatePresentation and SetTimer, answers them with the changed state (seq one higher) and sends
// that state to the page as the hub's "presentationState" broadcast. It records LeaveDeck and LeavePresentation. Any
// method can be made to fail with fail(), and a method the stub doesn't play is answered with the real hub's error for
// a missing method, so a test fails fast instead of waiting. It can hold joins and drop the connection, and it counts
// the WebSockets the page opens. It reloads the page, so call it before mount().
//
// With the audience option it also answers JoinAudience (or refuses it), records LeaveAudience and WatchAudience, and
// pushes "audienceEvent" messages with pushAudienceEvent. With the transports option it offers only those transports,
// and plays WebSockets, Server-Sent Events (a small local server the page's event stream is sent to) and long polling.
//
// Each hub method is one entry in the methods map below: a new method is a new entry.
export async function stubDeckHub(
  page: Page,
  { holdJoins = false, presentation, audience, transports }: StubDeckHubOptions = {},
) {
  const joined: unknown[] = [];
  const leftDecks: unknown[] = [];
  const held: (() => void)[] = [];
  const presentationJoins: StubPresentationJoin[] = [];
  const heldPresentationJoins: (() => void)[] = [];
  const updates: PresentationUpdate[] = [];
  const timers: TimerCommand[] = [];
  const leaves: unknown[] = [];
  const failures = new Map<string, { message: string; sticky: boolean }>();
  let refusing = presentation?.refuse ?? false;
  let current = presentation ? structuredClone(presentation.state) : undefined;
  let sockets = 0;
  let socket: Sink | undefined;
  const audienceJoins: string[] = [];
  const audienceLeaves: string[] = [];
  const watches: string[] = [];
  const transportsUsed: StubTransport[] = [];
  let audienceState = audience?.state && structuredClone(audience.state);
  let audienceRefusal = audience?.refuse;

  const send = (ws: Sink, message: object) => ws.send(`${JSON.stringify(message)}${RS}`);
  const broadcast = (state: PresentationState) =>
    socket && send(socket, { type: 1, target: 'presentationState', arguments: [state] });
  const change = (to: Partial<PresentationState>): PresentationState => {
    current = { ...current!, ...to, seq: current!.seq + 1 };
    return current;
  };

  // The presentation methods exist only with the presentation option.
  const presenting = (method: HubMethod): HubMethod | undefined =>
    presentation ? method : undefined;

  // The audience methods exist only with the audience option.
  const audienceMethod = (method: HubMethod): HubMethod | undefined =>
    audience ? method : undefined;

  const methods: Record<string, HubMethod | undefined> = {
    JoinDeck: {
      record: ([deckId]) => joined.push(deckId),
      answer: (_, reply) => {
        const complete = () => reply.result();
        if (holdJoins) held.push(complete);
        else complete();
      },
    },
    LeaveDeck: {
      record: ([deckId]) => leftDecks.push(deckId),
      answer: (_, reply) => reply.result(),
    },
    JoinPresentation: presenting({
      record: (args) => {
        const [deckId, clientId, role, label, timeZone, resume] = args as [
          string,
          string,
          string,
          string,
          string,
          unknown,
        ];
        presentationJoins.push({ deckId, clientId, role, label, timeZone, resume });
      },
      answer: (_, reply) => {
        // Captured now: a held join answers with the state as it was when the join arrived.
        const result = { state: current, clockMinutes: presentation?.clockMinutes ?? 872 };
        const answer = refusing ? () => reply.error(DECK_NOT_FOUND) : () => reply.result(result);
        if (presentation?.holdJoin) heldPresentationJoins.push(answer);
        else answer();
      },
    }),
    UpdatePresentation: presenting({
      record: (args) => updates.push(args[1] as PresentationUpdate),
      answer: (args, reply) => {
        const update = args[1] as PresentationUpdate;
        const state = change({
          slide: update.slide,
          step: update.step,
          black: update.black,
          skipDeepDives: update.skipDeepDives,
        });
        reply.result(state);
        broadcast(state);
      },
    }),
    SetTimer: presenting({
      record: (args) => timers.push(args[1] as TimerCommand),
      answer: (args, reply) => {
        const command = args[1] as TimerCommand;
        const { elapsedMs } = current!.timer;
        const timer =
          command === 'reset'
            ? { state: 'notStarted' as const, elapsedMs: 0 }
            : {
                state: command === 'pause' ? ('paused' as const) : ('running' as const),
                elapsedMs,
              };
        const state = change({ timer });
        reply.result(state);
        broadcast(state);
      },
    }),
    LeavePresentation: {
      record: ([deckId]) => leaves.push(deckId),
      answer: (_, reply) => reply.result(),
    },
    JoinAudience: audienceMethod({
      record: ([code]) => audienceJoins.push(code as string),
      answer: (_, reply) =>
        audienceRefusal ? reply.error(audienceRefusal) : reply.result(audienceState),
    }),
    LeaveAudience: audienceMethod({
      record: ([code]) => audienceLeaves.push(code as string),
      answer: (_, reply) => reply.result(),
    }),
    WatchAudience: audienceMethod({
      record: ([deckId]) => watches.push(deckId as string),
      answer: (_, reply) => reply.result(),
    }),
  };

  const invoke = (ws: Sink, message: HubMessage) => {
    const target = message.target ?? '';
    const args = message.arguments ?? [];
    // A send() (no invocationId) gets no completion.
    const complete = (body: object) =>
      message.invocationId !== undefined &&
      send(ws, { type: 3, invocationId: message.invocationId, ...body });
    const reply: Reply = {
      result: (value) => complete({ result: value }),
      error: (text) => complete({ error: hubError(target, text) }),
    };
    const method = Object.hasOwn(methods, target) ? methods[target] : undefined;
    if (!method) {
      complete({ error: missingMethod(target) });
      return;
    }
    method.record?.(args);
    const failure = failures.get(target);
    if (failure) {
      if (!failure.sticky) failures.delete(target);
      reply.error(failure.message);
      return;
    }
    method.answer(args, reply);
  };

  const offered = transports ?? (['WebSockets', 'ServerSentEvents', 'LongPolling'] as const);
  let startConnection = () => {};
  await page.route('**/hubs/deck/negotiate**', (route) => {
    // A new connection: its first long poll is answered at once again.
    startConnection();
    return route.fulfill({
      json: {
        negotiateVersion: 1,
        connectionId: 'conn-1',
        connectionToken: 'token-1',
        availableTransports: offered.map((transport) => ({
          transport,
          transferFormats: transport === 'WebSockets' ? ['Text', 'Binary'] : ['Text'],
        })),
      },
    });
  });

  // What the hub does with the frames a client sent, whichever transport carried them.
  const receive = (sink: Sink, raw: string) => {
    for (const frame of raw.split(RS).filter(Boolean)) {
      const message = JSON.parse(frame) as HubMessage;
      if (message.protocol !== undefined) sink.send(`{}${RS}`);
      else if (message.type === 1) invoke(sink, message);
    }
  };

  await page.routeWebSocket(/\/hubs\/deck\?/, (ws) => {
    sockets += 1;
    socket = ws;
    transportsUsed.push('WebSockets');
    ws.onMessage((raw) => receive(ws, String(raw)));
  });

  // Long polling: a GET is a poll (the first is answered at once, as the client waits for it to connect), a POST carries
  // the client's frames, a DELETE ends the connection. A poll is held until the hub has something to send.
  const queued: string[] = [];
  let heldPoll: (() => void) | undefined;
  let firstPoll = true;
  startConnection = () => {
    firstPoll = true;
    queued.length = 0;
  };
  const longPoll: Sink = {
    send(text) {
      queued.push(text);
      heldPoll?.();
    },
  };
  // Server-Sent Events: the page's event stream is sent to a local server that holds it open, because a fulfilled route
  // can't stream. The client's frames still arrive as POSTs, handled like the long poll's.
  let stream: import('node:http').ServerResponse | undefined;
  const eventStream: Sink = { send: (text) => stream?.write(`data: ${text}\n\n`) };
  // The server lives as long as the stream it serves: it closes when that stream ends (the client stopped, dropped or
  // the page went away) or when the page closes, so no server outlives its test even though the page is reused.
  // The browser reaches it at 127.0.0.1, so this needs the browser on the same host as the test, as it is in CI's
  // Playwright image; a remote browser (connectOptions) could not.
  const streamUrl = async () => {
    const server = createServer((request, response) => {
      response.writeHead(200, {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Access-Control-Allow-Origin': String(request.headers.origin ?? '*'),
        'Access-Control-Allow-Credentials': 'true',
      });
      response.write(':\n\n');
      stream = response;
      socket = eventStream;
      transportsUsed.push('ServerSentEvents');
      response.on('close', () => {
        if (stream === response) stream = undefined;
        closeServer();
      });
    });
    const closeServer = () => {
      page.off('close', closeServer);
      server.closeAllConnections();
      server.close();
    };
    page.once('close', closeServer);
    await new Promise<void>((done) => server.listen(0, '127.0.0.1', done));
    return `http://127.0.0.1:${(server.address() as AddressInfo).port}/`;
  };

  await page.route(/\/hubs\/deck\?/, async (route) => {
    const request = route.request();
    const method = request.method();
    if (method === 'POST') {
      const sink = socket === eventStream ? eventStream : longPoll;
      receive(sink, request.postData() ?? '');
      await route.fulfill({ status: 200, body: '' });
    } else if (method === 'DELETE') {
      await route.fulfill({ status: 202, body: '' });
    } else if ((await request.headerValue('accept'))?.includes('text/event-stream')) {
      await route.continue({ url: await streamUrl() });
    } else {
      if (firstPoll) {
        firstPoll = false;
        socket = longPoll;
        transportsUsed.push('LongPolling');
        await route.fulfill({ status: 200, body: '' });
        return;
      }
      if (queued.length === 0) await new Promise<void>((wake) => (heldPoll = wake));
      heldPoll = undefined;
      await route.fulfill({ status: 200, body: queued.splice(0).join('') });
    }
  });
  // The component-test page is loaded before the test starts, and the
  // WebSocket route only takes effect in a page loaded after it is set.
  await page.reload();
  return {
    joined,
    // Each LeaveDeck's deck id, in order.
    leftDecks,
    // Each JoinPresentation's arguments, in order, refused and failed ones included.
    presentationJoins,
    // Each UpdatePresentation's update, in order, failed ones included.
    updates,
    // Each SetTimer's command, in order, failed ones included.
    timers,
    // Each LeavePresentation's deck id, in order.
    leaves,
    // How many WebSockets the page has opened to the hub.
    get sockets() {
      return sockets;
    },
    // Each JoinAudience's code, in order, refused and failed ones included.
    audienceJoins,
    // Each LeaveAudience's code, in order.
    audienceLeaves,
    // Each WatchAudience's deck id, in order.
    watches,
    // The transport of each connection the page made, in order.
    transportsUsed,
    // Sends one "audienceEvent" to the newest connection. An audienceState also becomes the state later joins get.
    pushAudienceEvent(event: AudienceEvent) {
      if (event.type === 'audienceState') audienceState = structuredClone(event);
      send(socket!, { type: 1, target: 'audienceEvent', arguments: [event] });
    },
    // Changes the state later JoinAudience calls answer with, sending nothing: the state a reconnecting guest lands on.
    setAudienceState(state: AudienceState) {
      audienceState = structuredClone(state);
    },
    // Refuses every later JoinAudience with message (on), or answers them again (off).
    refuseAudience(message: StubAudienceOptions['refuse']) {
      audienceRefusal = message;
    },
    // Sends to the newest connection: the page's current deck.
    push(event: unknown) {
      send(socket!, { type: 1, target: 'deckEvent', arguments: [event] });
    },
    // Makes state the presentation's current state and broadcasts it to the newest connection as the hub does. A
    // later JoinPresentation answers with it.
    pushState(state: PresentationState) {
      current = structuredClone(state);
      broadcast(current);
    },
    // Makes the next call of target fail (every call, when sticky) as the real hub does when the method throws
    // HubException(message), such as DeckHub's "Not presenting". The call is still recorded, changes nothing and is
    // answered at once, even with holdJoins or holdJoin. A failure stays armed until it is used or stopFailing(target).
    fail(target: string, message: string, { sticky = false }: { sticky?: boolean } = {}) {
      failures.set(target, { message, sticky });
    },
    // Disarms fail(target).
    stopFailing(target: string) {
      failures.delete(target);
    },
    // Refuses every later JoinPresentation (on) or answers them again (off), as the hub does once a deck is deleted
    // or shared away mid-talk. Starts as the presentation option's refuse.
    refuseJoins(on: boolean) {
      refusing = on;
    },
    // Completes every JoinDeck held so far.
    releaseJoins() {
      for (const complete of held.splice(0)) complete();
    },
    // Answers every JoinPresentation held so far, each with the state as it was when that join arrived.
    releasePresentationJoins() {
      for (const answer of heldPresentationJoins.splice(0)) answer();
    },
    // Closes the newest connection the way a server going away does, letting
    // the client reconnect: SignalR's close message with allowReconnect.
    drop() {
      send(socket!, { type: 7, error: 'Server went away.', allowReconnect: true });
    },
  };
}

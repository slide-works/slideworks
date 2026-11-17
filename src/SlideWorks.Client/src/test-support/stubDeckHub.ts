import type { Page, WebSocketRoute } from 'playwright/test';
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

export interface StubDeckHubOptions {
  // Leave each JoinDeck unanswered until releaseJoins(), as a slow join would be.
  holdJoins?: boolean;
  // Play the presentation calls (contract P2) too. Without it, JoinPresentation, UpdatePresentation and SetTimer are
  // answered as a method the hub doesn't have.
  presentation?: StubPresentationOptions;
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

// Component tests only: plays the deck hub (/hubs/deck) at the network boundary, deck events and presentations both.
// It answers negotiate, the handshake and JoinDeck, records the deck ids each JoinDeck asked for, and pushes deckEvents
// to the client the way the server does. With the presentation option it also answers JoinPresentation (or refuses
// it), records each UpdatePresentation and SetTimer, answers them with the changed state (seq one higher) and sends
// that state to the page as the hub's "presentationState" broadcast. It records LeaveDeck and LeavePresentation. Any
// method can be made to fail with fail(), and a method the stub doesn't play is answered with the real hub's error for
// a missing method, so a test fails fast instead of waiting. It can hold joins and drop the connection, and it counts
// the WebSockets the page opens. It reloads the page, so call it before mount().
//
// Each hub method is one entry in the methods map below: a new method is a new entry.
export async function stubDeckHub(
  page: Page,
  { holdJoins = false, presentation }: StubDeckHubOptions = {},
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
  let socket: WebSocketRoute | undefined;

  const send = (ws: WebSocketRoute, message: object) => ws.send(`${JSON.stringify(message)}${RS}`);
  const broadcast = (state: PresentationState) =>
    socket && send(socket, { type: 1, target: 'presentationState', arguments: [state] });
  const change = (to: Partial<PresentationState>): PresentationState => {
    current = { ...current!, ...to, seq: current!.seq + 1 };
    return current;
  };

  // The presentation methods exist only with the presentation option.
  const presenting = (method: HubMethod): HubMethod | undefined =>
    presentation ? method : undefined;

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
  };

  const invoke = (ws: WebSocketRoute, message: HubMessage) => {
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

  await page.route('**/hubs/deck/negotiate**', (route) =>
    route.fulfill({
      json: {
        negotiateVersion: 1,
        connectionId: 'conn-1',
        connectionToken: 'token-1',
        availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }],
      },
    }),
  );
  await page.routeWebSocket(/\/hubs\/deck\?/, (ws) => {
    sockets += 1;
    socket = ws;
    ws.onMessage((raw) => {
      for (const frame of String(raw).split(RS).filter(Boolean)) {
        const message = JSON.parse(frame) as HubMessage;
        if (message.protocol !== undefined) ws.send(`{}${RS}`);
        else if (message.type === 1) invoke(ws, message);
      }
    });
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

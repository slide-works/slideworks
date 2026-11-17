import type { Page, WebSocketRoute } from 'playwright/test';
import type { PresentationState, PresentationUpdate, TimerCommand } from '../presentation/types';

// SignalR's JSON hub protocol ends every message with a record separator.
const RS = '\u001e';

// What the real hub sends for HubException("Deck not found") (DeckHub.DECK_NOT_FOUND, contract P2).
const REFUSED =
  "An unexpected error occurred invoking 'JoinPresentation' on the server. HubException: Deck not found";

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
  // Play the presentation calls (contract P2) too. Without it, the presentation calls are never answered.
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

// Component tests only: plays the deck hub (/hubs/deck) at the network boundary, deck events and presentations both.
// It answers negotiate, the handshake and JoinDeck, records the deck ids each JoinDeck asked for, and pushes deckEvents
// to the client the way the server does. With the presentation option it also answers JoinPresentation (or refuses
// it), records each UpdatePresentation and SetTimer, answers them with the changed state (seq one higher) and sends
// that state to the page as the hub's "presentationState" broadcast. It can hold joins and drop the connection, and it
// counts the WebSockets the page opens. It reloads the page, so call it before mount().
export async function stubDeckHub(
  page: Page,
  { holdJoins = false, presentation }: StubDeckHubOptions = {},
) {
  const joined: unknown[] = [];
  const held: (() => void)[] = [];
  const presentationJoins: StubPresentationJoin[] = [];
  const heldPresentationJoins: (() => void)[] = [];
  const updates: PresentationUpdate[] = [];
  const timers: TimerCommand[] = [];
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

  const onPresentation = (ws: WebSocketRoute, message: HubMessage) => {
    const args = message.arguments ?? [];
    const complete = (result: unknown) =>
      send(ws, { type: 3, invocationId: message.invocationId, result });
    if (!presentation || !current) return;
    if (message.target === 'JoinPresentation') {
      const [deckId, clientId, role, label, timeZone, resume] = args as [
        string,
        string,
        string,
        string,
        string,
        unknown,
      ];
      presentationJoins.push({ deckId, clientId, role, label, timeZone, resume });
      const answer = presentation.refuse
        ? () => send(ws, { type: 3, invocationId: message.invocationId, error: REFUSED })
        : (() => {
            const result = { state: current, clockMinutes: presentation.clockMinutes ?? 872 };
            return () => complete(result);
          })();
      if (presentation.holdJoin) heldPresentationJoins.push(answer);
      else answer();
    } else if (message.target === 'UpdatePresentation') {
      const update = args[1] as PresentationUpdate;
      updates.push(update);
      const state = change({
        slide: update.slide,
        step: update.step,
        black: update.black,
        skipDeepDives: update.skipDeepDives,
      });
      complete(state);
      broadcast(state);
    } else if (message.target === 'SetTimer') {
      const command = args[1] as TimerCommand;
      timers.push(command);
      const { elapsedMs } = current.timer;
      const timer =
        command === 'reset'
          ? { state: 'notStarted' as const, elapsedMs: 0 }
          : { state: command === 'pause' ? ('paused' as const) : ('running' as const), elapsedMs };
      const state = change({ timer });
      complete(state);
      broadcast(state);
    }
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
        else if (message.type === 1 && message.target === 'JoinDeck') {
          joined.push(message.arguments?.[0]);
          const complete = () => send(ws, { type: 3, invocationId: message.invocationId });
          if (holdJoins) held.push(complete);
          else complete();
        } else if (message.type === 1) onPresentation(ws, message);
      }
    });
  });
  // The component-test page is loaded before the test starts, and the
  // WebSocket route only takes effect in a page loaded after it is set.
  await page.reload();
  return {
    joined,
    // Each JoinPresentation's arguments, in order.
    presentationJoins,
    // Each UpdatePresentation's update, in order.
    updates,
    // Each SetTimer's command, in order.
    timers,
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

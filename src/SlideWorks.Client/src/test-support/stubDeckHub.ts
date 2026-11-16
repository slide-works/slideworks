import type { Page, WebSocketRoute } from 'playwright/test';

// SignalR's JSON hub protocol ends every message with a record separator.
const RS = '\u001e';

// Component tests only: plays the deck hub (/hubs/deck) at the network
// boundary. It answers negotiate, the handshake and JoinDeck, records the deck
// ids joined, and returns a function that pushes a deckEvent to the client the
// way the server does. It reloads the page, so call it before mount().
export async function stubDeckHub(page: Page) {
  const joined: unknown[] = [];
  let socket: WebSocketRoute | undefined;
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
    socket = ws;
    ws.onMessage((raw) => {
      for (const frame of String(raw).split(RS).filter(Boolean)) {
        const message = JSON.parse(frame) as {
          protocol?: string;
          type?: number;
          invocationId?: string;
          target?: string;
          arguments?: unknown[];
        };
        if (message.protocol !== undefined) ws.send(`{}${RS}`);
        else if (message.type === 1 && message.target === 'JoinDeck') {
          joined.push(message.arguments?.[0]);
          ws.send(`${JSON.stringify({ type: 3, invocationId: message.invocationId })}${RS}`);
        }
      }
    });
  });
  // The component-test page is loaded before the test starts, and the
  // WebSocket route only takes effect in a page loaded after it is set.
  await page.reload();
  return {
    joined,
    // Sends to the newest connection: the page's current deck.
    push(event: unknown) {
      socket!.send(`${JSON.stringify({ type: 1, target: 'deckEvent', arguments: [event] })}${RS}`);
    },
  };
}

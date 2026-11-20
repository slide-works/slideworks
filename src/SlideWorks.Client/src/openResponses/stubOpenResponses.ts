import type { Page, Route } from 'playwright/test';
import type {
  GuestResponseView,
  OpenResponseControlView,
  TextRequest,
  WordsRequest,
} from './types';

// One scripted answer to a PUT, such as { status: 409, json: { error: 'profane', message: '...' } }. Unscripted PUTs
// are stored and answered as the server does.
export type StubOpenResponseReply = { status: number; json: unknown };

export interface StubOpenResponsesOptions {
  // The join code the page is on. '482913' when left out, the guest fixtures' code.
  code?: string;
  slide: number;
  // What GET /api/join/{code}/slides/{slide}/response answers, and what an accepted PUT updates.
  view: GuestResponseView;
  // With control, GET /api/decks/{deckId}/slides/{slide}/response answers it, for the big screen.
  deckId?: string;
  control?: OpenResponseControlView;
}

// Plays the phone's open-response routes (SW-8 D13) for one slide: the guest GET, PUT .../words and PUT .../answer, and,
// with control, the presenter's GET. Call it before mount().
export async function stubOpenResponses(
  page: Page,
  { code = '482913', slide, view, deckId, control }: StubOpenResponsesOptions,
) {
  let current = structuredClone(view);
  let gets = 0;
  const sends: (WordsRequest | TextRequest)[] = [];
  const replies: StubOpenResponseReply[] = [];
  const base = `**/api/join/${encodeURIComponent(code)}/slides/${slide}`;

  const put = async (route: Route, store: (body: never) => unknown) => {
    const body = route.request().postDataJSON() as WordsRequest | TextRequest;
    sends.push(body);
    const scripted = replies.shift();
    if (scripted) return route.fulfill(scripted);
    return route.fulfill({ status: 200, json: store(body as never) });
  };

  await page.route(`${base}/response`, async (route) => {
    gets += 1;
    await route.fulfill({ status: 200, json: current });
  });
  await page.route(`${base}/words`, (route) =>
    put(route, (body: WordsRequest) => {
      if (current.kind === 'word-cloud') current = { ...current, mineWords: body.words };
      return { words: body.words };
    }),
  );
  await page.route(`${base}/answer`, (route) =>
    put(route, (body: TextRequest) => {
      if (current.kind === 'open-text') current = { ...current, mineText: body.text };
      return { text: body.text };
    }),
  );
  if (control !== undefined && deckId !== undefined)
    await page.route(`**/api/decks/${encodeURIComponent(deckId)}/slides/${slide}/response`, (route) =>
      route.fulfill({ status: 200, json: control }),
    );

  return {
    // How many guest GETs the page made.
    get gets() {
      return gets;
    },
    // Each PUT's body, in order.
    sends,
    // Answers the next PUT with reply instead of storing it.
    reply(reply: StubOpenResponseReply) {
      replies.push(reply);
    },
  };
}

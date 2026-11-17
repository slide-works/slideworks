import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PollControlView } from '../polls/types';
import { pollControlView } from './fixtures/pollStates';
import { stubPollControlApi } from './stubPollControlApi';

// The stub of the presenter's poll routes keeps poll state per slide and refuses what V4 refuses, so a panel or strip
// that offers a control in the wrong phase fails its test.

const deckId = 'deck-1';
const ROUTE = {
  open: 'open',
  close: 'close',
  reveal: 'reveal',
  showAnswer: 'answer',
  newRound: 'round',
};
type Answer = { status: number; body: PollControlView & { error?: string } };

// Sends one route from the page, as pollControlApi does: '' is the GET, an action name is its POST.
function send(page: Page, slide: number, action: keyof typeof ROUTE | '' = ''): Promise<Answer> {
  const path = `/api/decks/${deckId}/polls/${slide}${action === '' ? '' : `/${ROUTE[action]}`}`;
  return page.evaluate(
    async ([url, method]) => {
      const response = await fetch(url, { method });
      return { status: response.status, body: await response.json() };
    },
    [path, action === '' ? 'GET' : 'POST'] as const,
  );
}

const draft = (overrides: Partial<PollControlView> = {}) =>
  pollControlView('draft', { slide: 4, ...overrides });
const open = (overrides: Partial<PollControlView> = {}) =>
  pollControlView('open', { slide: 4, ...overrides });
const closed = (overrides: Partial<PollControlView> = {}) =>
  pollControlView('closed', { slide: 4, ...overrides });

async function expectRefused(page: Page, slide: number, action: keyof typeof ROUTE, code: string) {
  const before = (await send(page, slide)).body;
  const answer = await send(page, slide, action);
  expect(answer).toEqual({ status: 409, body: { error: code } });
  const after = (await send(page, slide)).body;
  expect({ ...after, seq: 0 }, `${action} changed nothing`).toEqual({ ...before, seq: 0 });
}

test('a read answers the slide poll with a higher seq, and 404 on a slide with no poll', async ({
  page,
}) => {
  await stubPollControlApi(page, deckId, { polls: [open()] });
  const first = await send(page, 4);
  expect(first.status).toBe(200);
  expect(first.body.phase).toBe('open');
  expect(first.body.seq).toBeGreaterThan(open().seq);
  expect((await send(page, 4)).body.seq).toBeGreaterThan(first.body.seq);
  expect((await send(page, 3)).status).toBe(404);
});

test('open moves a draft to open with a round, and refuses a second open with wrong_phase', async ({
  page,
}) => {
  await stubPollControlApi(page, deckId, { polls: [draft()] });
  const opened = await send(page, 4, 'open');
  expect(opened.status).toBe(200);
  expect(opened.body).toMatchObject({ phase: 'open', number: 1, answered: 0, revealed: false });
  expect(opened.body.round).not.toBeNull();
  // results: live, so the room sees the counts.
  expect(opened.body.roomSeesCounts).toBe(true);
  expect(opened.body.seq).toBeGreaterThan(draft().seq);
  await expectRefused(page, 4, 'open', 'wrong_phase');
});

test('open refuses not_live without an audience session', async ({ page }) => {
  const stub = await stubPollControlApi(page, deckId, { polls: [draft()], live: false });
  await expectRefused(page, 4, 'open', 'not_live');
  stub.setLive(true);
  expect((await send(page, 4, 'open')).body.phase).toBe('open');
});

test("open refuses another_poll_open while another slide's round is open", async ({ page }) => {
  await stubPollControlApi(page, deckId, { polls: [open(), draft({ slide: 2 })] });
  await expectRefused(page, 2, 'open', 'another_poll_open');
});

test('close only from open: the round joins rounds and the room sees the counts', async ({
  page,
}) => {
  await stubPollControlApi(page, deckId, { polls: [draft({ slide: 2 }), open()] });
  await expectRefused(page, 2, 'close', 'wrong_phase');
  const done = await send(page, 4, 'close');
  expect(done.status).toBe(200);
  expect(done.body).toMatchObject({ phase: 'closed', roomSeesCounts: true, number: 2 });
  expect(done.body.rounds).toEqual([...open().rounds, { number: 2, answered: 21, joined: 41 }]);
  await expectRefused(page, 4, 'close', 'wrong_phase');
});

test('reveal only from open, already_visible when the room sees the counts', async ({ page }) => {
  await stubPollControlApi(page, deckId, {
    polls: [open(), closed({ slide: 2 }), open({ slide: 3, roomSeesCounts: true })],
  });
  await expectRefused(page, 2, 'reveal', 'wrong_phase');
  await expectRefused(page, 3, 'reveal', 'already_visible');
  const shown = await send(page, 4, 'reveal');
  expect(shown.body).toMatchObject({ phase: 'open', revealed: true, roomSeesCounts: true });
  await expectRefused(page, 4, 'reveal', 'already_visible');
});

test('show answer only from closed, no_correct with no correct option', async ({ page }) => {
  const noCorrect = closed({ slide: 2, answerShown: false });
  noCorrect.settings = { ...noCorrect.settings, correct: null };
  await stubPollControlApi(page, deckId, {
    polls: [open(), noCorrect, closed({ slide: 3, answerShown: false })],
  });
  await expectRefused(page, 4, 'showAnswer', 'wrong_phase');
  await expectRefused(page, 2, 'showAnswer', 'no_correct');
  expect((await send(page, 3, 'showAnswer')).body).toMatchObject({
    phase: 'closed',
    answerShown: true,
  });
});

test('new round only from closed: number + 1, open on arrival, draft when manual', async ({
  page,
}) => {
  const onArrival = closed();
  const manual = closed({ slide: 2 });
  manual.settings = { ...manual.settings, opens: 'manual' };
  await stubPollControlApi(page, deckId, { polls: [onArrival, manual, open({ slide: 3 })] });
  await expectRefused(page, 3, 'newRound', 'wrong_phase');
  await send(page, 3, 'close');

  const again = await send(page, 4, 'newRound');
  expect(again.body).toMatchObject({
    phase: 'open',
    number: 2,
    answered: 0,
    answerShown: false,
    revealed: false,
    rounds: onArrival.rounds,
  });
  expect(again.body.round).not.toBeNull();
  expect(again.body.counts).toEqual([0, 0, 0, 0]);

  await send(page, 4, 'close');
  const later = await send(page, 2, 'newRound');
  expect(later.body).toMatchObject({ phase: 'draft', number: 2, round: null, answered: 0 });
});

test("opening a poll discards another slide's draft from New round, back to its closed round", async ({
  page,
}) => {
  const manual = closed({ slide: 2 });
  manual.settings = { ...manual.settings, opens: 'manual' };
  await stubPollControlApi(page, deckId, { polls: [manual, draft()] });
  expect((await send(page, 2, 'newRound')).body).toMatchObject({ phase: 'draft', number: 2 });
  expect((await send(page, 4, 'open')).body.phase).toBe('open');
  expect((await send(page, 2)).body).toMatchObject({ phase: 'closed', number: 1 });
  // The discarded draft used up number 2, so the next round on slide 2 is 3.
  await send(page, 4, 'close');
  expect((await send(page, 2, 'newRound')).body).toMatchObject({ phase: 'draft', number: 3 });
});

test("new round refuses another_poll_open while another slide's round is open", async ({
  page,
}) => {
  await stubPollControlApi(page, deckId, { polls: [closed(), open({ slide: 3 })] });
  await expectRefused(page, 4, 'newRound', 'another_poll_open');
});

test("new round discards another slide's draft from New round first, as open does", async ({
  page,
}) => {
  const manual = closed({ slide: 2 });
  manual.settings = { ...manual.settings, opens: 'manual' };
  await stubPollControlApi(page, deckId, { polls: [manual, closed()] });
  expect((await send(page, 2, 'newRound')).body.phase).toBe('draft');
  expect((await send(page, 4, 'newRound')).body).toMatchObject({ phase: 'open', number: 2 });
  expect((await send(page, 2)).body).toMatchObject({ phase: 'closed', number: 1 });
});

test('ending the session closes the open round', async ({ page }) => {
  const stub = await stubPollControlApi(page, deckId, { polls: [open()] });
  stub.setLive(false);
  expect((await send(page, 4)).body).toMatchObject({ phase: 'closed', roomSeesCounts: true });
});

test('a scripted route replaces the answer and changes nothing; network-down drops it', async ({
  page,
}) => {
  const stub = await stubPollControlApi(page, deckId, {
    polls: [open()],
    routes: { close: { status: 500, json: { error: 'boom' } }, reveal: 'network-down' },
  });
  expect(await send(page, 4, 'close')).toEqual({ status: 500, body: { error: 'boom' } });
  await expect(send(page, 4, 'reveal')).rejects.toThrow();
  expect(stub.poll(4)?.phase).toBe('open');
  expect(stub.calls).toEqual([
    { call: 'close', slide: 4 },
    { call: 'reveal', slide: 4 },
  ]);
});

test('hold delays the next answer of a route until release', async ({ page }) => {
  const stub = await stubPollControlApi(page, deckId, { polls: [open()] });
  const held = stub.hold('close');
  const answer = send(page, 4, 'close');
  await expect.poll(() => stub.calls.length).toBe(1);
  // The state changed on arrival; only the answer waits.
  expect(stub.poll(4)?.phase).toBe('closed');
  let settled = false;
  void answer.then(() => (settled = true));
  await page.evaluate(() => new Promise((done) => setTimeout(done, 100)));
  expect(settled).toBe(false);
  held.release();
  expect((await answer).body.phase).toBe('closed');
});

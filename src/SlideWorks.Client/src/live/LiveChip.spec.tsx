import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { JoinProvider } from '../slides/JoinContext';
import { guestView } from '../test-support/fixtures/guestStates';
import { deckPlay } from '../test-support/fixtures/screens';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { LiveChip } from './LiveChip';
import type { LiveView } from './liveView';

const idle: LiveView = {
  sessionId: null,
  code: deckPlay.join.code,
  codeLabel: deckPlay.join.codeLabel,
  joinUrl: deckPlay.join.joinUrl,
  hostLabel: deckPlay.join.hostLabel,
  live: false,
  joinedCount: 0,
  joinBar: false,
};
const open: LiveView = { ...idle, sessionId: 'session-1', live: true };

type Calls = { method: string; body: unknown }[];

// Plays /live and /look at the network boundary. read answers GET /live: a LiveView, or the status of a refusal.
async function stubLive(
  page: Page,
  read: LiveView | number,
  options: { goLive?: LiveView | number; end?: number; look?: number } = {},
): Promise<Calls> {
  const calls: Calls = [];
  await page.route(`**/api/decks/${deckPlay.id}/live`, async (route) => {
    const method = route.request().method();
    calls.push({ method, body: null });
    const answer = (value: LiveView | number | undefined, fallback: LiveView | number) => {
      const given = value ?? fallback;
      return typeof given === 'number'
        ? route.fulfill({ status: given, json: { error: 'refused' } })
        : route.fulfill({ json: given });
    };
    if (method === 'GET') return answer(read, read);
    if (method === 'POST') return answer(options.goLive, open);
    return route.fulfill({ status: options.end ?? 204 });
  });
  await page.route(`**/api/decks/${deckPlay.id}/look`, (route) => {
    calls.push({ method: 'PUT', body: route.request().postDataJSON() });
    return route.fulfill({ status: options.look ?? 200, json: {} });
  });
  return calls;
}

function chip(owner: boolean, live = false) {
  return (
    <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live }} owner={owner}>
      <LiveChip deckTitle={deckPlay.title} />
    </JoinProvider>
  );
}

const hubOptions = { audience: { state: guestView('live').state } };

test('LiveChip renders for the owner once GET live answers 200', async ({ mount, page }) => {
  await stubDeckHub(page, hubOptions);
  await stubLive(page, idle);
  await mount(chip(true));
  await expect(page.getByTestId('live-chip')).toBeVisible();
});

for (const status of [403, 404]) {
  test(`LiveChip renders nothing when GET live answers ${status}`, async ({ mount, page }) => {
    await stubDeckHub(page, hubOptions);
    await stubLive(page, status);
    await mount(chip(true));
    await expect(page.getByTestId('live-chip')).toHaveCount(0);
  });
}

test('LiveChip renders nothing for a viewer who cannot be the owner', async ({ mount, page }) => {
  await stubDeckHub(page, hubOptions);
  const calls = await stubLive(page, idle);
  await mount(chip(false));
  await expect(page.getByTestId('live-chip')).toHaveCount(0);
  expect(calls).toHaveLength(0);
});

test('LiveChip goes live and ends from its buttons, named for the deck', async ({
  mount,
  page,
}) => {
  await stubDeckHub(page, hubOptions);
  const calls = await stubLive(page, idle);
  await mount(chip(true));
  await expect(page.getByTestId('live-chip-end')).toHaveCount(0);
  await page.getByRole('button', { name: `Go live for ${deckPlay.title}` }).click();
  await expect(page.getByTestId('live-chip-code')).toHaveText(deckPlay.join.codeLabel);
  await expect(page.getByTestId('live-chip-count')).toHaveText('0 joined');
  await expect(page.getByTestId('live-chip-join-bar')).toBeVisible();
  await page.getByRole('button', { name: `End live session for ${deckPlay.title}` }).click();
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
  await expect(page.getByTestId('live-chip-end')).toHaveCount(0);
  expect(calls.map((call) => call.method)).toEqual(['GET', 'POST', 'DELETE']);
});

test('LiveChip shows a message when Go live fails, and clears it on the next try', async ({
  mount,
  page,
}) => {
  await stubDeckHub(page, hubOptions);
  await stubLive(page, idle, { goLive: 500 });
  await mount(chip(true));
  await expect(page.getByTestId('live-chip-error')).toHaveCount(0);
  await page.getByTestId('live-chip-go-live').click();
  await expect(page.getByTestId('live-chip-error')).toHaveText('Could not go live.');
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
});

test('LiveChip shows a message when End fails and stays live', async ({ mount, page }) => {
  await stubDeckHub(page, hubOptions);
  await stubLive(page, open, { end: 500 });
  await mount(chip(true, true));
  await page.getByTestId('live-chip-end').click();
  await expect(page.getByTestId('live-chip-error')).toHaveText('Could not end the live session.');
  await expect(page.getByTestId('live-chip-end')).toBeVisible();
});

test('LiveChip join bar toggle sends the look and reflects the deck after a read', async ({
  mount,
  page,
}) => {
  await stubDeckHub(page, hubOptions);
  const calls = await stubLive(page, { ...open, joinBar: true });
  await mount(chip(true, true));
  const toggle = page.getByTestId('live-chip-join-bar');
  await expect(toggle).toHaveAttribute('aria-pressed', 'true');
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-pressed', 'false');
  expect(calls.at(-1)).toEqual({ method: 'PUT', body: { chrome: { joinBar: false } } });
});

test('LiveChip shows a message when the join bar change fails', async ({ mount, page }) => {
  await stubDeckHub(page, hubOptions);
  await stubLive(page, open, { look: 500 });
  await mount(chip(true, true));
  await page.getByTestId('live-chip-join-bar').click();
  await expect(page.getByTestId('live-chip-error')).toHaveText('Could not change the join bar.');
  await expect(page.getByTestId('live-chip-join-bar')).toHaveAttribute('aria-pressed', 'false');
});

test('LiveChip count follows joinedCount events from WatchAudience', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, hubOptions);
  await stubLive(page, open);
  await mount(chip(true, true));
  await expect(page.getByTestId('live-chip-count')).toHaveText('0 joined');
  await expect.poll(() => hub.watches).toEqual([deckPlay.id]);
  hub.pushAudienceEvent({ type: 'joinedCount', count: 37 });
  await expect(page.getByTestId('live-chip-count')).toHaveText('37 joined');
});

test('LiveChip follows the deck ending from another window', async ({ mount, page }) => {
  await stubDeckHub(page, hubOptions);
  let ended = false;
  await page.route(`**/api/decks/${deckPlay.id}/live`, (route) =>
    route.fulfill({ json: ended ? idle : open }),
  );
  const component = await mount(chip(true, true));
  await expect(page.getByTestId('live-chip-end')).toBeVisible();
  ended = true;
  // The page re-reads on liveChanged and hands the provider a join that is no longer live.
  await component.update(chip(true, false));
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
});

test('LiveChip touch targets are at least 48px', async ({ mount, page }) => {
  await stubDeckHub(page, hubOptions);
  await stubLive(page, idle);
  await mount(chip(true));
  await page.getByTestId('live-chip-go-live').click();
  for (const id of ['live-chip-join-bar', 'live-chip-end']) {
    const box = await page.getByTestId(id).boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(48);
    expect(box?.width).toBeGreaterThanOrEqual(48);
  }
});

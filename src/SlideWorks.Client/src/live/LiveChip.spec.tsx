import { test, expect } from '@playwright/experimental-ct-react';
import { JoinProvider } from '../slides/JoinContext';
import { deckPlay } from '../test-support/fixtures/screens';
import { LiveChip } from './LiveChip';

function chip(owner: boolean) {
  return (
    <JoinProvider deckId={deckPlay.id} join={deckPlay.join} owner={owner}>
      <LiveChip deckTitle={deckPlay.title} />
    </JoinProvider>
  );
}

test('LiveChip renders for the owner', async ({ mount, page }) => {
  await mount(chip(true));
  await expect(page.getByTestId('live-chip')).toBeVisible();
});

test('LiveChip renders nothing for anyone else', async ({ mount, page }) => {
  await mount(chip(false));
  await expect(page.getByTestId('live-chip')).toHaveCount(0);
});

test('LiveChip goes live and ends from its buttons, named for the deck', async ({
  mount,
  page,
}) => {
  await mount(chip(true));
  await expect(page.getByTestId('live-chip-end')).toHaveCount(0);
  await page.getByRole('button', { name: `Go live for ${deckPlay.title}` }).click();
  await expect(page.getByTestId('live-chip-code')).toHaveText(deckPlay.join.codeLabel);
  await expect(page.getByTestId('live-chip-count')).toHaveText('0 joined');
  await expect(page.getByTestId('live-chip-join-bar')).toBeVisible();
  await page.getByRole('button', { name: `End live session for ${deckPlay.title}` }).click();
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
  await expect(page.getByTestId('live-chip-end')).toHaveCount(0);
});

test('LiveChip touch targets are at least 48px', async ({ mount, page }) => {
  await mount(chip(true));
  await page.getByTestId('live-chip-go-live').click();
  for (const id of ['live-chip-join-bar', 'live-chip-end']) {
    const box = await page.getByTestId(id).boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(48);
    expect(box?.width).toBeGreaterThanOrEqual(48);
  }
});

test('LiveChip starts live when the deck is live through the API', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live: true }} owner>
      <LiveChip deckTitle={deckPlay.title} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('live-chip-end')).toBeVisible();
});

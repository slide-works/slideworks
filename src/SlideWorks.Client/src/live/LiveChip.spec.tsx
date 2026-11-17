import { test, expect } from '@playwright/experimental-ct-react';
import { JoinProvider } from '../slides/JoinContext';
import { deckPlay } from '../test-support/fixtures/screens';
import { LiveChip } from './LiveChip';

test('LiveChip renders for the owner', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={deckPlay.join} owner>
      <LiveChip deckTitle={deckPlay.title} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('live-chip')).toBeVisible();
});

test('LiveChip renders nothing for anyone else', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={deckPlay.join} owner={false}>
      <LiveChip deckTitle={deckPlay.title} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('live-chip')).toHaveCount(0);
});

test('LiveChip goes live and ends from its button, named for the deck', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={deckPlay.join} owner>
      <LiveChip deckTitle={deckPlay.title} />
    </JoinProvider>,
  );
  await page.getByRole('button', { name: `Go live for ${deckPlay.title}` }).click();
  await expect(page.getByTestId('live-chip-count')).toHaveText('0');
  await page.getByRole('button', { name: `End live session for ${deckPlay.title}` }).click();
  await expect(page.getByTestId('live-chip-count')).toHaveCount(0);
});

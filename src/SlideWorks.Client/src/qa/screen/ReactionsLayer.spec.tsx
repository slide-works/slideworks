import { test, expect } from '@playwright/experimental-ct-react';
import { Player } from '../../player/Player';
import { deckPlayWithQa } from '../../test-support/fixtures/screens';
import { mountPlayer } from '../../test-support/mountPlayer';
import { stubApi } from '../../test-support/stubApi';
import { QA_REACTIONS_LAYER_TEST_IDS as IDS } from '../testIds';
import type { ReactionEmoji } from '../types';
import { ScreenReactionsFixture } from './ScreenReactionsFixture';
import type { ScreenBurst } from './useScreenReactions';

// The floating reactions over the player (SW-9 Q9; mockup screen state 5), through the fixture provider.
test.use({ viewport: { width: 1920, height: 1080 } });

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
});

const bursts = (count: number, emoji: ReactionEmoji = 'clap', from = 1): ScreenBurst[] =>
  Array.from({ length: count }, (_, i) => ({ id: from + i, emoji }));

const player = (list: ScreenBurst[] | null) => (
  <ScreenReactionsFixture bursts={list}>
    <Player deck={deckPlayWithQa()} />
  </ScreenReactionsFixture>
);

async function mountWith(
  mount: Parameters<typeof mountPlayer>[0],
  page: Parameters<typeof mountPlayer>[1],
  list: ScreenBurst[] | null,
) {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  return mountPlayer(mount, page, player(list));
}

test('a burst of 3 floats 3 emoji and no chip', async ({ mount, page }) => {
  await mountWith(mount, page, bursts(3, 'heart'));

  await expect(page.getByTestId(IDS.float)).toHaveCount(3);
  await expect(page.getByTestId(IDS.float).first()).toHaveText('❤️');
  await expect(page.getByTestId(IDS.chip)).toHaveCount(0);
});

test('a burst of 44 floats 30 and folds 14 into a chip', async ({ mount, page }) => {
  await mountWith(mount, page, bursts(44));

  await expect(page.getByTestId(IDS.float)).toHaveCount(30);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+14 👏');
});

test('a later burst floats and the earlier ones are not drawn again', async ({ mount, page }) => {
  const component = await mountWith(mount, page, bursts(2));
  await expect(page.getByTestId(IDS.float)).toHaveCount(2);

  await component.update(player([...bursts(2), ...bursts(1, 'wow', 3)]));

  await expect(page.getByTestId(IDS.float)).toHaveCount(3);
});

test('the floats are the cap in total: a later batch past 30 folds into a chip', async ({
  mount,
  page,
}) => {
  const component = await mountWith(mount, page, bursts(28));
  await component.update(player([...bursts(28), ...bursts(5, 'laugh', 29)]));

  await expect(page.getByTestId(IDS.float)).toHaveCount(30);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+3 😂');
});

test('a float leaves the screen when its animation ends', async ({ mount, page }) => {
  await mountWith(mount, page, bursts(2));

  await expect(page.getByTestId(IDS.float)).toHaveCount(2);
  await expect(page.getByTestId(IDS.float)).toHaveCount(0, { timeout: 6000 });
});

test('the chip goes after a few seconds', async ({ mount, page }) => {
  await mountWith(mount, page, bursts(31));

  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 👏');
  await expect(page.getByTestId(IDS.chip)).toHaveCount(0, { timeout: 6000 });
});

test('reduced motion floats nothing and shows only the chip', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mountPlayer(mount, page, player(bursts(3)));

  await expect(page.getByTestId(IDS.layer)).toBeAttached();
  await expect(page.getByTestId(IDS.float)).toHaveCount(0);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+3 👏');
});

test('nothing renders for a null screen', async ({ mount, page }) => {
  await mountWith(mount, page, null);

  await expect(page.getByTestId('slide-1')).toBeVisible();
  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
});

test('screenshot the floating reactions', async ({ mount, page }) => {
  await mountWith(mount, page, bursts(12, 'thumbsUp'));

  await expect(page.getByTestId(IDS.float)).toHaveCount(12);
  await expect(page.getByTestId('player-canvas')).toHaveScreenshot('reactions-float.png', {
    maxDiffPixels: 4000,
    animations: 'disabled',
  });
});

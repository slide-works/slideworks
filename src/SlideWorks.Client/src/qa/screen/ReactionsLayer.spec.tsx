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

// A page whose clock the test drives: nothing moves until runFor. The first render holds no bursts, so no timer starts
// before the clock is paused.
async function mountClocked(
  mount: Parameters<typeof mountPlayer>[0],
  page: Parameters<typeof mountPlayer>[1],
  reduced: boolean,
) {
  await page.emulateMedia({ reducedMotion: reduced ? 'reduce' : 'no-preference' });
  await page.clock.install({ time: 0 });
  const component = await mountPlayer(mount, page, player([]));
  await page.clock.pauseAt(60_000);
  return component;
}

test('a chip clears 2.5 s after its own last tap, and another emoji does not extend it', async ({
  mount,
  page,
}) => {
  const component = await mountClocked(mount, page, true);
  await component.update(player(bursts(1, 'clap', 1)));
  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 👏');

  await page.clock.runFor(1500);
  await component.update(player([...bursts(1, 'clap', 1), ...bursts(1, 'heart', 2)]));
  await expect(page.getByTestId(IDS.chip)).toHaveText(['+1 ❤️', '+1 👏']);

  await page.clock.runFor(900);
  await expect(page.getByTestId(IDS.chip)).toHaveText(['+1 ❤️', '+1 👏']);
  await page.clock.runFor(150);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 ❤️');

  await page.clock.runFor(1350);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 ❤️');
  await page.clock.runFor(100);
  await expect(page.getByTestId(IDS.chip)).toHaveCount(0);
});

test('a tap on a chip restarts that chip only', async ({ mount, page }) => {
  const component = await mountClocked(mount, page, true);
  await component.update(player(bursts(1, 'clap', 1)));
  await page.clock.runFor(1000);
  await component.update(player([...bursts(1, 'clap', 1), ...bursts(1, 'heart', 2)]));
  await page.clock.runFor(1000);
  await component.update(
    player([...bursts(1, 'clap', 1), ...bursts(1, 'heart', 2), ...bursts(1, 'clap', 3)]),
  );
  await expect(page.getByTestId(IDS.chip)).toHaveText(['+1 ❤️', '+2 👏']);

  // The heart goes at 3500 (2.5 s after 1000), the claps at 4500 (2.5 s after 2000).
  await page.clock.runFor(1450);
  await expect(page.getByTestId(IDS.chip)).toHaveText(['+1 ❤️', '+2 👏']);
  await page.clock.runFor(100);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+2 👏');
  await page.clock.runFor(900);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+2 👏');
  await page.clock.runFor(100);
  await expect(page.getByTestId(IDS.chip)).toHaveCount(0);
});

test('two chips show at once, one per emoji', async ({ mount, page }) => {
  await mountWith(mount, page, [
    ...bursts(31, 'clap', 1),
    ...bursts(1, 'heart', 32),
    ...bursts(1, 'wow', 33),
  ]);

  await expect(page.getByTestId(IDS.float)).toHaveCount(30);
  await expect(page.getByTestId(IDS.chip)).toHaveText(['+1 ❤️', '+1 😮', '+1 👏']);
});

test('a float whose animation never ends is retired by its timer, and frees its slot', async ({
  mount,
  page,
}) => {
  const component = await mountClocked(mount, page, false);
  // A background tab runs no animations: no animationend comes.
  await page.addStyleTag({
    content: `[data-testid="${IDS.float}"] { animation: none !important; }`,
  });
  await component.update(player(bursts(31)));
  await expect(page.getByTestId(IDS.float)).toHaveCount(30);
  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 👏');

  await page.clock.runFor(2900);
  await expect(page.getByTestId(IDS.float)).toHaveCount(30);
  await page.clock.runFor(200);
  await expect(page.getByTestId(IDS.float)).toHaveCount(0);

  await component.update(player([...bursts(31), ...bursts(1, 'wow', 32)]));
  await expect(page.getByTestId(IDS.float)).toHaveText('😮');
});

test('bursts whose ids start again after the list emptied are drawn', async ({ mount, page }) => {
  const component = await mountWith(mount, page, bursts(2));
  await expect(page.getByTestId(IDS.float)).toHaveCount(2);

  await component.update(player([]));
  await component.update(player(bursts(1, 'wow', 1)));

  await expect(page.getByTestId(IDS.float)).toHaveCount(3);
  await expect(page.getByTestId(IDS.float).last()).toHaveText('😮');
});

test('a float rises in one of five lanes by its id', async ({ mount, page }) => {
  await mountWith(mount, page, bursts(6));

  await expect(page.getByTestId(IDS.float)).toHaveCount(6);
  const rights = await page
    .getByTestId(IDS.float)
    .evaluateAll((els) => els.map((el) => getComputedStyle(el).right));
  expect(rights).toEqual(['84px', '120px', '156px', '192px', '48px', '84px']);
});

test('a change of the reduced-motion setting mid-session applies to the next tap', async ({
  mount,
  page,
}) => {
  const component = await mountWith(mount, page, bursts(1, 'clap', 1));
  await expect(page.getByTestId(IDS.float)).toHaveCount(1);

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await component.update(player([...bursts(1, 'clap', 1), ...bursts(1, 'heart', 2)]));
  await expect(page.getByTestId(IDS.chip)).toHaveText('+1 ❤️');
  await expect(page.getByTestId(IDS.float)).toHaveCount(1);

  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await component.update(
    player([...bursts(1, 'clap', 1), ...bursts(1, 'heart', 2), ...bursts(1, 'wow', 3)]),
  );
  await expect(page.getByTestId(IDS.float)).toHaveCount(2);
});

// animations: 'disabled' would finish each rise, leaving every float faded out at the top. Instead the paused clock holds
// the retire timers, and each float's rise is paused at its own point so the shot shows a spread up the lanes.
test('screenshot the floating reactions', async ({ mount, page }) => {
  const component = await mountClocked(mount, page, false);
  await component.update(player(bursts(12, 'thumbsUp')));

  await expect(page.getByTestId(IDS.float)).toHaveCount(12);
  await page.getByTestId(IDS.float).evaluateAll((els) =>
    els.forEach((el, i) =>
      el.getAnimations().forEach((animation) => {
        animation.pause();
        animation.currentTime = 100 + i * 90;
      }),
    ),
  );
  await expect(page.getByTestId('player-canvas')).toHaveScreenshot('reactions-float.png', {
    maxDiffPixels: 4000,
    animations: 'allow',
  });
});

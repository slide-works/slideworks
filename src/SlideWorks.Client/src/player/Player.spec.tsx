import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { sampleDeck } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { Player } from './Player';

// The sample deck "Ship smaller": 16 slides, and slide 3 (bullets) has 4 steps.
const TOTAL = sampleDeck.slides.length;

// A deep link as the browser would load it, set before the player mounts.
async function openAt(page: Page, hash: string) {
  await page.evaluate((h) => window.history.replaceState(null, '', h), hash);
}

async function expectOnSlide(page: Page, slide: number) {
  await expect(page.getByTestId(`slide-${slide}`)).toBeVisible();
  await expect(page.getByTestId('player-count')).toHaveText(`${slide} / ${TOTAL}`);
  for (const other of [1, 2, 3, 4, 5].filter((n) => n !== slide)) {
    await expect(page.getByTestId(`slide-${other}`)).toBeHidden();
  }
}

async function expectHash(page: Page, hash: string) {
  await expect.poll(() => page.evaluate(() => window.location.hash)).toBe(hash);
}

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
});

test('deep link #3.2 opens slide 3 step 2', async ({ mount, page }) => {
  await openAt(page, '#3.2');
  await mount(<Player deck={sampleDeck} />);

  await expectOnSlide(page, 3);
  await expect(page.getByTestId('player-steps')).toHaveAttribute('aria-label', 'Step 2 of 4');
  await expectHash(page, '#3.2');
});

test('invalid hash falls back to slide 1', async ({ mount, page }) => {
  for (const hash of ['#99', '#0', '#abc', '#3.x']) {
    await openAt(page, hash);
    const player = await mount(<Player deck={sampleDeck} />);

    await expectOnSlide(page, 1);
    await expectHash(page, '#1');
    await player.unmount();
  }
});

test('arrow right reveals steps then advances one slide', async ({ mount, page }) => {
  await openAt(page, '#3');
  await mount(<Player deck={sampleDeck} />);
  const historyLength = await page.evaluate(() => window.history.length);

  for (const step of [1, 2, 3, 4]) {
    await page.keyboard.press('ArrowRight');
    await expectHash(page, `#3.${step}`);
    await expectOnSlide(page, 3);
  }
  await page.keyboard.press('ArrowRight');
  await expectHash(page, '#4');
  await expectOnSlide(page, 4);

  // Back goes onto slide 3 with every step shown, then takes one step off.
  await page.keyboard.press('ArrowLeft');
  await expectHash(page, '#3.4');
  await page.keyboard.press('ArrowLeft');
  await expectHash(page, '#3.3');

  // Every move replaced the hash: no history entry per slide or step.
  expect(await page.evaluate(() => window.history.length)).toBe(historyLength);
});

test('the other keys, buttons and click thirds move one step or slide each', async ({
  mount,
  page,
}) => {
  await openAt(page, '#2');
  await mount(<Player deck={sampleDeck} />);

  await page.keyboard.press('PageDown');
  await expectHash(page, '#3');
  await page.keyboard.press('Space');
  await expectHash(page, '#3.1');
  await page.keyboard.press('Shift+Space');
  await expectHash(page, '#3');
  await page.keyboard.press('PageUp');
  await expectHash(page, '#2');
  await page.keyboard.press('End');
  await expectHash(page, `#${TOTAL}`);
  await page.keyboard.press('Home');
  await expectHash(page, '#1');

  await page.getByTestId('player-next').click();
  await expectHash(page, '#2');
  await page.getByTestId('player-previous').click();
  await expectHash(page, '#1');

  const stage = page.getByTestId('player-stage');
  const box = (await stage.boundingBox())!;
  await stage.click({ position: { x: box.width * 0.9, y: box.height / 2 } });
  await expectHash(page, '#2');
  await stage.click({ position: { x: box.width * 0.5, y: box.height / 2 } });
  await expectHash(page, '#2');
  await stage.click({ position: { x: box.width * 0.1, y: box.height / 2 } });
  await expectHash(page, '#1');
});

test('a swipe over 50 px moves one slide, a shorter one does not', async ({ mount, page }) => {
  await openAt(page, '#2');
  await mount(<Player deck={sampleDeck} />);
  const swipe = (dx: number) =>
    page.getByTestId('player-stage').evaluate((stage, distance) => {
      const touch = (x: number) =>
        new Touch({ identifier: 1, target: stage, clientX: x, clientY: 300 });
      stage.dispatchEvent(
        new TouchEvent('touchstart', { bubbles: true, changedTouches: [touch(400)] }),
      );
      stage.dispatchEvent(
        new TouchEvent('touchend', { bubbles: true, changedTouches: [touch(400 + distance)] }),
      );
    }, dx);

  await swipe(-40);
  await expectHash(page, '#2');
  await swipe(-80);
  await expectHash(page, '#3');
  await swipe(80);
  await expectHash(page, '#2');
});

test('editing the hash navigates', async ({ mount, page }) => {
  await mount(<Player deck={sampleDeck} />);
  await expectOnSlide(page, 1);

  await page.evaluate(() => {
    window.location.hash = '#5';
  });
  await expectOnSlide(page, 5);

  await page.evaluate(() => {
    window.location.hash = '#3.2';
  });
  await expectOnSlide(page, 3);
  await expect(page.getByTestId('player-steps')).toHaveAttribute('aria-label', 'Step 2 of 4');

  // Back returns to the hash before, through popstate.
  await page.goBack();
  await expectOnSlide(page, 5);
});

test('keys ignored inside a textarea', async ({ mount, page }) => {
  await mount(
    <div>
      <textarea data-testid="notes" />
      <div contentEditable data-testid="editable" />
      <Player deck={sampleDeck} />
    </div>,
  );

  await page.getByTestId('notes').focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('Space');
  await page.keyboard.press('PageDown');
  // The space reached the textarea, so the player returned before preventDefault.
  await expect(page.getByTestId('notes')).toHaveValue(' ');

  await page.getByTestId('editable').focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('End');

  await expectOnSlide(page, 1);
  await expectHash(page, '#1');
});

test('IME composition does not navigate', async ({ mount, page }) => {
  await mount(<Player deck={sampleDeck} />);
  const keydown = (init: { key: string; isComposing?: boolean; keyCode?: number }) =>
    page.evaluate((options) => document.body.dispatchEvent(new KeyboardEvent('keydown', options)), {
      bubbles: true,
      cancelable: true,
      ...init,
    });

  await keydown({ key: 'ArrowRight', isComposing: true });
  await keydown({ key: ' ', isComposing: true });
  await keydown({ key: 'ArrowRight', keyCode: 229 });
  await expectOnSlide(page, 1);
  await expectHash(page, '#1');

  // The same key outside composition moves, so the listener was live.
  await keydown({ key: 'ArrowRight' });
  await expectOnSlide(page, 2);
});

test('every slide stays rendered', async ({ mount, page }) => {
  await openAt(page, '#4');
  await mount(<Player deck={sampleDeck} />);

  await expect(page.locator('[data-testid^="player-slide-"]')).toHaveCount(TOTAL);
  for (const slide of sampleDeck.slides) {
    const layer = page.getByTestId(`player-slide-${slide.number}`);
    await expect(page.getByTestId(`slide-${slide.number}`)).toBeAttached();
    if (slide.number === 4) {
      await expect(layer).toBeVisible();
      await expect(layer).not.toHaveAttribute('aria-hidden');
      continue;
    }
    await expect(layer).toBeHidden();
    await expect(layer).toHaveAttribute('aria-hidden', 'true');
    await expect(layer).toHaveCSS('visibility', 'hidden');
    await expect(layer).toHaveCSS('display', 'block');
  }
});

test('phone 390px scales without horizontal overflow', async ({ mount, page }) => {
  for (const width of [390, 320]) {
    await page.setViewportSize({ width, height: 844 });
    const player = await mount(<Player deck={sampleDeck} />);

    const slide = page.getByTestId('slide-1');
    await expect(slide).toBeVisible();
    const box = (await slide.boundingBox())!;
    // One uniform scale: the whole width, at 16:9, centred vertically on the stage.
    expect(box.x).toBeCloseTo(0, 0);
    expect(box.width).toBeCloseTo(width, 0);
    expect(box.height).toBeCloseTo((width * 1080) / 1920, 0);
    const stage = (await page.getByTestId('player-stage').boundingBox())!;
    expect(box.y - stage.y).toBeCloseTo(stage.y + stage.height - (box.y + box.height), 0);

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBe(0);
    await player.unmount();
  }
});

test('the canvas rescales when the window resizes', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1600, height: 800 });
  await mount(<Player deck={sampleDeck} />);
  const stage = page.getByTestId('player-stage');
  const slide = page.getByTestId('slide-1');

  // Height-bound on a wide window: the slide fills the stage's height, centred across.
  const stageBox = (await stage.boundingBox())!;
  await expect
    .poll(async () => (await slide.boundingBox())!.height)
    .toBeCloseTo(stageBox.height, 0);
  const box = (await slide.boundingBox())!;
  expect(box.width).toBeCloseTo((stageBox.height * 1920) / 1080, 0);
  expect(box.x).toBeCloseTo((1600 - box.width) / 2, 0);

  await page.setViewportSize({ width: 600, height: 800 });
  await expect.poll(async () => (await slide.boundingBox())!.width).toBeCloseTo(600, 0);
});

test('fullscreen button requests fullscreen', async ({ mount, page }) => {
  await mount(<Player deck={sampleDeck} />);
  await page.evaluate(() => {
    const calls: string[] = [];
    (window as unknown as { fullscreenCalls: string[] }).fullscreenCalls = calls;
    Element.prototype.requestFullscreen = function () {
      calls.push(this.getAttribute('data-testid') ?? '');
      return Promise.resolve();
    };
  });

  await page.getByTestId('player-fullscreen').click();
  await page.keyboard.press('f');

  expect(
    await page.evaluate(() => (window as unknown as { fullscreenCalls: string[] }).fullscreenCalls),
  ).toEqual(['player', 'player']);
});

test('reduced motion disables transitions', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  const player = await mount(<Player deck={sampleDeck} />);
  await expect(page.getByTestId('player-slide-1')).not.toHaveCSS('transition-duration', '0s');
  await expect(page.getByTestId('player-progress')).not.toHaveCSS('transition-duration', '0s');
  await player.unmount();

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mount(<Player deck={sampleDeck} />);
  await expect(page.getByTestId('player-slide-1')).toHaveCSS('transition-duration', '0s');
  await expect(page.getByTestId('player-slide-2')).toHaveCSS('transition-duration', '0s');
  await expect(page.getByTestId('player-progress')).toHaveCSS('transition-duration', '0s');
});

import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { LAYOUT_SLOTS } from '../test-support/fixtures/layoutSlots';
import { sampleDeck } from '../test-support/fixtures/screens';
import { mountPlayer } from '../test-support/mountPlayer';
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

// The session history entry's key: a replaceState keeps it, a push makes a new
// one. (history.length can't tell here: the CT page is reused across tests and
// Chromium caps it at 50.)
function entryKey(page: Page) {
  return page.evaluate(
    () =>
      (window as unknown as { navigation: { currentEntry: { key: string } } }).navigation
        .currentEntry.key,
  );
}

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
});

test('deep link #3.2 opens slide 3 step 2', async ({ mount, page }) => {
  await openAt(page, '#3.2');
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await expectOnSlide(page, 3);
  await expect(page.getByTestId('player-steps')).toHaveAttribute('aria-label', 'Step 2 of 4');
  await expectHash(page, '#3.2');
});

test('invalid hash falls back to slide 1', async ({ mount, page }) => {
  for (const hash of ['#99', '#0', '#abc', '#3.x']) {
    await openAt(page, hash);
    const player = await mountPlayer(mount, page, <Player deck={sampleDeck} />);

    await expectOnSlide(page, 1);
    await expectHash(page, '#1');
    await player.unmount();
  }
});

test('arrow right reveals steps then advances one slide', async ({ mount, page }) => {
  await openAt(page, '#3');
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  const key = await entryKey(page);

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

  // Every move replaced the hash: still the same history entry.
  expect(await entryKey(page)).toBe(key);
});

test('a held key moves one step, however many times it repeats', async ({ mount, page }) => {
  await openAt(page, '#3');
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await page.evaluate(() => {
    const seen: { repeat: boolean; prevented: boolean }[] = [];
    (window as unknown as { keysSeen: typeof seen }).keysSeen = seen;
    // Added after the player's own listener, so it sees what the player did.
    window.addEventListener('keydown', (event) =>
      seen.push({ repeat: event.repeat, prevented: event.defaultPrevented }),
    );
  });

  // Holding the key: one keydown, then auto-repeats until it is let go.
  await page.keyboard.down('ArrowRight');
  await page.keyboard.down('ArrowRight');
  await page.keyboard.down('ArrowRight');
  await page.keyboard.up('ArrowRight');

  await expectHash(page, '#3.1');
  await expect(page.getByTestId('player-steps')).toHaveAttribute('aria-label', 'Step 1 of 4');
  // The repeats were swallowed too, so the page never scrolls under a held key.
  expect(
    await page.evaluate(
      () => (window as unknown as { keysSeen: { repeat: boolean; prevented: boolean }[] }).keysSeen,
    ),
  ).toEqual([
    { repeat: false, prevented: true },
    { repeat: true, prevented: true },
    { repeat: true, prevented: true },
  ]);

  // The next press moves again.
  await page.keyboard.press('ArrowRight');
  await expectHash(page, '#3.2');
});

test('the other keys, buttons and click thirds move one step or slide each', async ({
  mount,
  page,
}) => {
  await openAt(page, '#2');
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

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
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
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

test('a two-finger touch is not a swipe', async ({ mount, page }) => {
  await openAt(page, '#2');
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  // A pinch: one finger down, a second joins, then they lift one at a time,
  // each far enough from where the first one started to pass for a swipe.
  await page.getByTestId('player-stage').evaluate((stage) => {
    const finger = (identifier: number, x: number) =>
      new Touch({ identifier, target: stage, clientX: x, clientY: 300 });
    const send = (type: string, touches: Touch[], changedTouches: Touch[]) =>
      stage.dispatchEvent(new TouchEvent(type, { bubbles: true, touches, changedTouches }));
    send('touchstart', [finger(1, 400)], [finger(1, 400)]);
    send('touchstart', [finger(1, 400), finger(2, 500)], [finger(2, 500)]);
    send('touchend', [finger(2, 600)], [finger(1, 300)]);
    send('touchend', [], [finger(2, 600)]);
  });
  await expectHash(page, '#2');

  // A one-finger swipe afterwards still moves.
  await page.getByTestId('player-stage').evaluate((stage) => {
    const finger = (x: number) =>
      new Touch({ identifier: 3, target: stage, clientX: x, clientY: 300 });
    stage.dispatchEvent(
      new TouchEvent('touchstart', {
        bubbles: true,
        touches: [finger(400)],
        changedTouches: [finger(400)],
      }),
    );
    stage.dispatchEvent(
      new TouchEvent('touchend', { bubbles: true, touches: [], changedTouches: [finger(320)] }),
    );
  });
  await expectHash(page, '#3');
});

test('editing the hash navigates', async ({ mount, page }) => {
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
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
  await mountPlayer(
    mount,
    page,
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

  // Out of the fields, the same key moves, so the listener was live.
  await page.getByTestId('editable').blur();
  await page.keyboard.press('ArrowRight');
  await expectOnSlide(page, 2);
});

test('keys ignored inside an input, a select and a role=textbox', async ({ mount, page }) => {
  await mountPlayer(
    mount,
    page,
    <div>
      <input data-testid="field" />
      <select data-testid="choice" defaultValue="a">
        <option value="a">A</option>
        <option value="b">B</option>
      </select>
      <div role="textbox" tabIndex={0} data-testid="custom-box">
        <span tabIndex={0} data-testid="inside-box">
          inside
        </span>
      </div>
      <Player deck={sampleDeck} />
    </div>,
  );

  await page.getByTestId('field').focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('Space');
  await page.keyboard.press('End');
  // The space reached the input, so the player returned before preventDefault.
  await expect(page.getByTestId('field')).toHaveValue(' ');

  await page.getByTestId('choice').focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('PageDown');
  await page.keyboard.press('End');

  await page.getByTestId('custom-box').focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('Space');
  await page.getByTestId('inside-box').focus();
  await page.keyboard.press('PageDown');

  await expectOnSlide(page, 1);
  await expectHash(page, '#1');

  // Out of the fields, the same key moves, so the listener was live.
  await page.getByTestId('inside-box').blur();
  await page.keyboard.press('ArrowRight');
  await expectOnSlide(page, 2);
});

test('IME composition does not navigate', async ({ mount, page }) => {
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
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
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);

  await expect(page.locator('[data-testid^="player-slide-"]')).toHaveCount(TOTAL);
  for (const slide of sampleDeck.slides) {
    const layer = page.getByTestId(`player-slide-${slide.number}`);
    await expect(page.getByTestId(`slide-${slide.number}`)).toBeAttached();
    if (slide.number === 4) {
      await expect(layer).toBeVisible();
      await expect(layer).not.toHaveAttribute('aria-hidden');
      await expect(layer).not.toHaveAttribute('inert');
      continue;
    }
    await expect(layer).toBeHidden();
    await expect(layer).toHaveAttribute('aria-hidden', 'true');
    // React 19 renders the boolean prop as the bare attribute.
    await expect(layer).toHaveAttribute('inert', '');
    await expect(layer).toHaveCSS('visibility', 'hidden');
    await expect(layer).toHaveCSS('display', 'block');
  }
});

test('a deck that changes under the player keeps a valid position', async ({ mount, page }) => {
  await openAt(page, `#${TOTAL}`);
  const player = await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await expectOnSlide(page, TOTAL);
  const key = await entryKey(page);

  // A live refetch drops every slide after 5: the player lands on the new last slide.
  const firstFive = { ...sampleDeck, slides: sampleDeck.slides.slice(0, 5) };
  await player.update(<Player deck={firstFive} />);
  await expect(page.getByTestId('player-count')).toHaveText('5 / 5');
  await expect(page.getByTestId('slide-5')).toBeVisible();
  await expectHash(page, '#5');
  expect(await entryKey(page)).toBe(key);

  // On slide 3 at its last step, slide 3 loses two items: the step is clamped.
  await page.evaluate(() => {
    window.history.replaceState(window.history.state, '', '#3.4');
    window.dispatchEvent(new HashChangeEvent('hashchange'));
  });
  await expect(page.getByTestId('player-steps')).toHaveAttribute('aria-label', 'Step 4 of 4');
  const shorterSlide3 = {
    ...firstFive,
    slides: firstFive.slides.map((slide) => (slide.number === 3 ? { ...slide, steps: 2 } : slide)),
  };
  await player.update(<Player deck={shorterSlide3} />);
  await expect(page.getByTestId('player-steps')).toHaveAttribute('aria-label', 'Step 2 of 2');
  await expectHash(page, '#3.2');
  await expect(page.getByTestId('slide-3')).toBeVisible();
  // Both clamps replaced the hash: still the same history entry.
  expect(await entryKey(page)).toBe(key);
});

test('phone 390px scales without horizontal overflow', async ({ mount, page }) => {
  for (const width of [390, 320]) {
    await page.setViewportSize({ width, height: 844 });
    const player = await mountPlayer(mount, page, <Player deck={sampleDeck} />);

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
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
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
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
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

test('fullscreen button says exit while fullscreen', async ({ mount, page }) => {
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  const button = page.getByTestId('player-fullscreen');
  await expect(button).toHaveAccessibleName('Fullscreen');

  await button.click();
  await expect
    .poll(() => page.evaluate(() => document.fullscreenElement?.getAttribute('data-testid')))
    .toBe('player');
  await expect(button).toHaveAccessibleName('Exit fullscreen');

  await button.click();
  await expect.poll(() => page.evaluate(() => document.fullscreenElement)).toBeNull();
  await expect(button).toHaveAccessibleName('Fullscreen');
});

test('a refused fullscreen request is caught', async ({ mount, page }) => {
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await page.evaluate(() => {
    const state = { requests: 0, unhandled: 0 };
    (window as unknown as { fullscreenState: typeof state }).fullscreenState = state;
    window.addEventListener('unhandledrejection', () => state.unhandled++);
    Element.prototype.requestFullscreen = function () {
      state.requests++;
      return Promise.reject(new TypeError('Permissions check failed'));
    };
  });

  await page.getByTestId('player-fullscreen').click();
  await page.keyboard.press('f');
  // Let any unhandled rejection be reported before reading the count.
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 100)));

  expect(
    await page.evaluate(() => (window as unknown as { fullscreenState: unknown }).fullscreenState),
  ).toEqual({ requests: 2, unhandled: 0 });
  await expect(page.getByTestId('player-fullscreen')).toHaveAccessibleName('Fullscreen');
});

test('reduced motion disables transitions', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  const player = await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await expect(page.getByTestId('player-slide-1')).not.toHaveCSS('transition-duration', '0s');
  await expect(page.getByTestId('player-progress')).not.toHaveCSS('transition-duration', '0s');
  await player.unmount();

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mountPlayer(mount, page, <Player deck={sampleDeck} />);
  await expect(page.getByTestId('player-slide-1')).toHaveCSS('transition-duration', '0s');
  await expect(page.getByTestId('player-slide-2')).toHaveCSS('transition-duration', '0s');
  await expect(page.getByTestId('player-progress')).toHaveCSS('transition-duration', '0s');
});

test('link opens in a new tab and does not advance the slide', async ({ mount, page }) => {
  const title =
    'Small batches show where work waits, so every merge stays small and reviewable'.slice(0, 80);
  const slots = {
    headline: LAYOUT_SLOTS.links.headline,
    links: [{ title, url: 'https://example.com/the-link' }],
  };
  const deck = structuredClone(sampleDeck);
  deck.slides = deck.slides.map((slide) =>
    slide.number === 2 ? { ...slide, layout: 'links', steps: 0, slots } : slide,
  );
  await page
    .context()
    .route('https://example.com/**', (route) =>
      route.fulfill({ contentType: 'text/html', body: '<p>linked</p>' }),
    );
  await openAt(page, '#2');
  await mountPlayer(mount, page, <Player deck={deck} />);
  await expectOnSlide(page, 2);

  const anchor = page.getByTestId('links-title');
  const stage = (await page.getByTestId('player-stage').boundingBox())!;
  const box = (await anchor.boundingBox())!;
  const clickX = box.x + box.width - 20;
  expect((clickX - stage.x) / stage.width).toBeGreaterThan(2 / 3);

  const popup = page.waitForEvent('popup');
  await anchor.click({ position: { x: box.width - 20, y: box.height / 2 } });
  expect((await popup).url()).toBe('https://example.com/the-link');
  await expectHash(page, '#2');
  await expectOnSlide(page, 2);
});

test('a video plays in the slide, links to YouTube, and stops when the slide is left', async ({
  mount,
  page,
}) => {
  const id = 'M7lc1UVf-VE';
  const given = `https://www.youtube.com/watch?v=${id}&list=PLsecret`;
  const deck = structuredClone(sampleDeck);
  deck.slides = deck.slides.map((slide) =>
    slide.number === 2
      ? {
          ...slide,
          layout: 'video',
          steps: 0,
          slots: { headline: 'Watch the slice ship', url: given, caption: 'Two minutes' },
        }
      : slide,
  );
  await page.route('https://i.ytimg.com/**', (route) => route.abort());
  await page.route('https://www.youtube-nocookie.com/**', (route) => route.abort());
  await page.context().route('https://www.youtube.com/**', (route) =>
    route.fulfill({ contentType: 'text/html', body: '<p>youtube</p>' }),
  );
  await openAt(page, '#2');
  await mountPlayer(mount, page, <Player deck={deck} />);
  await expectOnSlide(page, 2);
  await expect(page.getByTestId('video-frame')).toHaveCount(0);

  await page.getByTestId('video-play').click();
  await expect(page.getByTestId('video-frame')).toHaveAttribute(
    'src',
    `https://www.youtube-nocookie.com/embed/${id}?rel=0&modestbranding=1&playsinline=1&autoplay=1`,
  );
  await expectHash(page, '#2');

  const popup = page.waitForEvent('popup');
  await page.getByTestId('video-youtube').click();
  expect((await popup).url()).toBe(`https://www.youtube.com/watch?v=${id}`);
  await (await popup).close();
  await expectHash(page, '#2');

  await page.keyboard.press('ArrowRight');
  await expectOnSlide(page, 3);
  await expect(page.getByTestId('video-frame')).toHaveCount(0);

  await page.keyboard.press('ArrowLeft');
  await expectOnSlide(page, 2);
  await expect(page.getByTestId('video-frame')).toHaveCount(1);
});

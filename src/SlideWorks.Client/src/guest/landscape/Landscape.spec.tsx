import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { GUEST_TEST_IDS } from '../testIds';
import { LandscapeLayer } from './LandscapeLayer';
import { PortraitHint } from './PortraitHint';

const LANDSCAPE = { width: 844, height: 390 };
const PORTRAIT = { width: 390, height: 844 };

const layer = (
  <LandscapeLayer index={3} count={12} connected progress={0.25} languageChip={<span>ไทย</span>} />
);

// Fullscreen support is read at render, so the stand-in goes in before the page loads. noRequest is a browser that
// reports fullscreenEnabled but has no requestFullscreen.
async function stubFullscreen(page: Page, mode: 'supported' | 'unsupported' | 'noRequest') {
  const request = {
    supported: '() => Promise.resolve()',
    unsupported: 'undefined',
    noRequest: 'undefined',
  }[mode];
  // A script string: a function passed to addInitScript goes through the bundler first.
  await page.addInitScript(`
    Object.defineProperty(document, 'fullscreenEnabled', { value: ${mode !== 'unsupported'}, configurable: true });
    Object.defineProperty(HTMLElement.prototype, 'requestFullscreen', { value: ${request}, configurable: true, writable: true });
  `);
}

const tapSurface = (page: Page) => page.getByRole('button', { name: 'Show slide controls' });

test('Landscape_WhenSlideTapped_ShouldShowOverlayForThreeSeconds', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  const overlay = page.getByTestId(GUEST_TEST_IDS.landscapeOverlay);
  await expect(overlay).toBeHidden();

  await tapSurface(page).click();
  await expect(overlay).toBeVisible();
  await expect(overlay).toContainText('3 / 12');
  await expect(overlay).toContainText('Live');
  await expect(overlay).toContainText('ไทย');
  await page.clock.runFor(2900);
  await expect(overlay).toBeVisible();
  await page.clock.runFor(200);
  await expect(overlay).toBeHidden();
});

test('Landscape_WhenTappedTwice_ShouldHide', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  const overlay = page.getByTestId(GUEST_TEST_IDS.landscapeOverlay);
  await tapSurface(page).click();
  await expect(overlay).toBeVisible();
  await tapSurface(page).click({ position: { x: 400, y: 200 } });
  await expect(overlay).toBeHidden();
});

test('Landscape_WhenFullscreenSupported_ShouldShowButton', async ({ mount, page }) => {
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await tapSurface(page).click();
  await expect(page.getByRole('button', { name: 'Full screen' })).toBeVisible();
});

test('Landscape_WhenFullscreenUnsupported_ShouldHideButton', async ({ mount, page }) => {
  await stubFullscreen(page, 'unsupported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await tapSurface(page).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen)).toHaveCount(0);
});

test('Landscape_WhenFullscreenRefused_ShouldShowMessage', async ({ mount, page }) => {
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  // The request is read when the button is pressed, so the refusal can go in after the mount.
  await page.evaluate(() => {
    document.documentElement.requestFullscreen = () => Promise.reject(new Error('refused'));
  });
  await tapSurface(page).click();
  await page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeFullscreenError)).toHaveText(
    "Full screen isn't available here",
  );
});

test('Landscape_WhenRequestFullscreenMissing_ShouldHideButton', async ({ mount, page }) => {
  await stubFullscreen(page, 'noRequest');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await tapSurface(page).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen)).toHaveCount(0);
});

test('Landscape_WhenOverlayReopenedAfterRefusal_ShouldNotShowMessage', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await page.evaluate(() => {
    document.documentElement.requestFullscreen = () => Promise.reject(new Error('refused'));
  });
  await tapSurface(page).click();
  await page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeFullscreenError)).toBeVisible();
  await page.clock.runFor(3100);
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeHidden();

  await tapSurface(page).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeFullscreenError)).toHaveCount(0);
});

test('Landscape_WhenFocusInsideOverlay_ShouldStayUntilFocusLeaves', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  const overlay = page.getByTestId(GUEST_TEST_IDS.landscapeOverlay);
  const button = page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen);
  await tapSurface(page).click();
  await page.keyboard.press('Tab');
  await expect(button).toBeFocused();
  await page.clock.runFor(5000);
  await expect(overlay).toBeVisible();
  await expect(button).toBeFocused();

  await button.blur();
  await page.clock.runFor(3100);
  await expect(overlay).toBeHidden();
});

test('Landscape_WhenFullscreenPressedWithPointer_ShouldHideAfterThreeSeconds', async ({
  mount,
  page,
}) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await tapSurface(page).click();
  await page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen).click();
  await page.clock.runFor(3100);
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeHidden();
});

test('Landscape_WhenHiddenByTapWithFocusInside_ShouldFocusTapSurface', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await tapSurface(page).click();
  await page.getByTestId(GUEST_TEST_IDS.landscapeFullscreen).focus();
  await tapSurface(page).click({ position: { x: 400, y: 200 } });
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeHidden();
  await expect(tapSurface(page)).toBeFocused();
});

test('Landscape_WhenPortrait_ShouldShowNoLayer', async ({ mount, page }) => {
  await page.setViewportSize(PORTRAIT);
  await mount(layer);
  await expect(tapSurface(page)).toBeHidden();
  await expect(page.getByText('ไทย')).toBeHidden();
});

test('Landscape_WhenOverlayShown_ShouldMatchScreenshot', async ({ mount, page }) => {
  await stubFullscreen(page, 'supported');
  await page.setViewportSize(LANDSCAPE);
  await mount(layer);
  await tapSurface(page).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.landscapeOverlay)).toBeVisible();
  await expect(page).toHaveScreenshot('landscape-overlay.png', { maxDiffPixels: 100 });
});

test('PortraitHint_WhenClosed_ShouldNotReturn', async ({ mount, page }) => {
  await page.setViewportSize(PORTRAIT);
  await mount(
    <>
      <PortraitHint />
      <button type="button">Next control</button>
    </>,
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveText(
    /Turn sideways to read the slide full screen/,
  );
  await page.getByRole('button', { name: 'Close hint' }).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Next control' })).toBeFocused();

  await page.reload();
  await mount(<PortraitHint />);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveCount(0);
});

test('PortraitHint_WhenPhoneTurned_ShouldNotReturn', async ({ mount, page }) => {
  await page.setViewportSize(PORTRAIT);
  await mount(<PortraitHint />);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toBeVisible();

  await page.setViewportSize(LANDSCAPE);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toBeHidden();
  // The media query reports its change on the next frame.
  await page.evaluate(() => new Promise((done) => requestAnimationFrame(() => done(true))));
  await page.setViewportSize(PORTRAIT);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveCount(0);

  await page.reload();
  await mount(<PortraitHint />);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveCount(0);
});

test('PortraitHint_WhenNothingFollows_ShouldFocusParentOnClose', async ({ mount, page }) => {
  await page.setViewportSize(PORTRAIT);
  await mount(<PortraitHint />);
  await page.getByRole('button', { name: 'Close hint' }).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveCount(0);
  await expect(page.locator('#root')).toBeFocused();
});

test('PortraitHint_WhenInertContentFollows_ShouldFocusNextRealControl', async ({ mount, page }) => {
  await page.setViewportSize(PORTRAIT);
  await mount(
    <>
      <PortraitHint />
      <div inert>
        <a href="#mirror">Mirror link</a>
      </div>
      <button type="button">Real control</button>
    </>,
  );
  await page.getByRole('button', { name: 'Close hint' }).click();
  await expect(page.getByRole('button', { name: 'Real control' })).toBeFocused();
});

test('PortraitHint_WhenTabletPortrait_ShouldBeHidden', async ({ mount, page }) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await mount(<PortraitHint />);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toBeHidden();
});

test('PortraitHint_WhenStorageThrows_ShouldStillRender', async ({ mount, page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'localStorage', {
      get() {
        throw new Error('storage blocked');
      },
      configurable: true,
    });
  });
  await page.setViewportSize(PORTRAIT);
  await mount(<PortraitHint />);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toBeVisible();
  await page.getByRole('button', { name: 'Close hint' }).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toHaveCount(0);
});

test('PortraitHint_WhenShown_ShouldMatchScreenshot', async ({ mount, page }) => {
  await page.setViewportSize(PORTRAIT);
  await mount(<PortraitHint />);
  await expect(page.getByTestId(GUEST_TEST_IDS.portraitHint)).toBeVisible();
  await expect(page).toHaveScreenshot('portrait-hint.png', { maxDiffPixels: 100 });
});

import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { LookView } from '../../../api/types';
import { lookStyle } from '../../../look/lookStyle';
import { lookViewOf } from '../../../test-support/fixtures/lookViewOf';
import { SlideFrame, type SlideFrameProps } from '../../SlideFrame';

test.use({ viewport: { width: 1920, height: 1080 } });

// A 1x1 PNG; the test images need to load.
const PIXEL =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';
const LIGHT = '/api/templates/tpl_t/assets/light';
const DARK = '/api/templates/tpl_t/assets/dark';
const BROKEN = '/api/templates/tpl_t/assets/broken';

async function serveLogos(page: Page) {
  const png = Buffer.from(PIXEL, 'base64');
  await page.route('**/api/templates/tpl_t/assets/*', (route) =>
    route.request().url().endsWith('/broken')
      ? route.fulfill({ status: 404 })
      : route.fulfill({ status: 200, contentType: 'image/png', body: png }),
  );
}

function look(overrides: Partial<LookView['logos']> = {}, deckLogo: string | null = null) {
  return lookViewOf('talk', 'cobalt', {
    logo: deckLogo === null ? null : { url: deckLogo },
    logos: { light: LIGHT, dark: DARK, showOnDark: true, ...overrides },
  });
}

// A plain function, not a component: CT can only mount components from source files.
function slide(view: LookView, props: Partial<Omit<SlideFrameProps, 'children'>> = {}) {
  return (
    <div style={lookStyle(view)}>
      <SlideFrame
        layout="bullets"
        surface="light"
        part={null}
        number={3}
        total={16}
        parts={[]}
        deckTitle="Ship smaller"
        author="Sam Rivera"
        look={view}
        deepDive={false}
        titleStamp={null}
        {...props}
      >
        <h2>Every slip we had last year started as a big batch</h2>
      </SlideFrame>
    </div>
  );
}

test('shows the dark logo in the corner of a title slide', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look(), { layout: 'title', surface: 'dark', number: 1 }));

  const corner = page.getByTestId('corner-logo');
  await expect(corner).toBeVisible();
  await expect(corner).toHaveAttribute('alt', '');
  await expect(corner).toHaveAttribute('src', DARK);
  const box = (await corner.boundingBox())!;
  expect(box.height).toBe(34);
  expect(box.x + box.width).toBe(1920 - 120);
  expect(box.y + box.height / 2).toBeCloseTo(1023 + 8.5, 0);
  await expect(page.getByTestId('template-logo-light')).toHaveCount(0);
});

test('shows it on divider and close', async ({ mount, page }) => {
  await serveLogos(page);
  for (const layout of ['divider', 'close'] as const) {
    const component = await mount(slide(look(), { layout, surface: 'dark', part: 1 }));
    await expect(page.getByTestId('corner-logo')).toBeVisible();
    await component.unmount();
  }
});

test('hides the corner logo when showOnDark is off', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look({ showOnDark: false }), { layout: 'title', surface: 'dark' }));

  await expect(page.getByTestId('corner-logo')).toHaveCount(0);
});

test('hides the corner logo on a join slide', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look(), { layout: 'join', surface: 'dark' }));

  await expect(page.getByTestId('corner-logo')).not.toBeVisible();
});

test('shows the light logo in the footer of a light slide', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look()));

  const logo = page.getByTestId('template-logo-light');
  await expect(logo).toBeVisible();
  await expect(logo).toHaveAttribute('alt', '');
  await expect(logo).toHaveAttribute('src', LIGHT);
  expect((await logo.boundingBox())!.height).toBe(34);
  await expect(page.getByTestId('template-logo-dark')).not.toBeVisible();
  await expect(page.getByTestId('corner-logo')).not.toBeVisible();
});

test('shows the dark logo in the footer of a dark bullets slide', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look(), { surface: 'dark' }));

  await expect(page.getByTestId('template-logo-dark')).toBeVisible();
  await expect(page.getByTestId('template-logo-light')).not.toBeVisible();
  await expect(page.getByTestId('corner-logo')).not.toBeVisible();
});

test('shows no template logo when the deck has its own', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look({}, LIGHT)));

  await expect(page.getByTestId('slide-logo')).toBeVisible();
  await expect(page.getByTestId('template-logo-light')).toHaveCount(0);
  await expect(page.getByTestId('template-logo-dark')).toHaveCount(0);
});

test('shows no footer logo on dark when the template has no dark logo', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look({ dark: null }), { surface: 'dark' }));

  await expect(page.getByTestId('template-logo-dark')).toHaveCount(0);
  await expect(page.getByTestId('template-logo-light')).not.toBeVisible();
});

test('renders nothing for a built-in look', async ({ mount, page }) => {
  for (const layout of ['bullets', 'title'] as const) {
    const component = await mount(
      slide(lookViewOf('talk', 'cobalt'), {
        layout,
        surface: layout === 'title' ? 'dark' : 'light',
      }),
    );
    await expect(page.locator('.sw-tlogo')).toHaveCount(0);
    await component.unmount();
  }
});

test('hides an image that fails to load', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look({ light: BROKEN })));

  await expect(page.getByTestId('template-logo-light')).toHaveCount(0);
  await expect(page.getByTestId('template-logo-dark')).toHaveCount(1);
});

test('keeps the text line at 1023 px with a logo', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look()));
  await expect(page.getByTestId('template-logo-light')).toBeVisible();

  const text = (await page.getByTestId('slide-footer-text').boundingBox())!;
  expect(text.y).toBe(1023);
});

test('keeps the draft chip beside the logo at the right end', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide({ ...look(), draft: true }));

  const chip = (await page.getByTestId('draft-chip').boundingBox())!;
  const logo = (await page.getByTestId('template-logo-light').boundingBox())!;
  expect(chip.x + chip.width).toBeLessThanOrEqual(logo.x);
  expect(logo.x - (chip.x + chip.width)).toBe(18);
  expect(logo.x + logo.width).toBe(1920 - 120);
});

test('screenshot: title slide with the corner logo', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look(), { layout: 'title', surface: 'dark', number: 1 }));
  await expect(page.getByTestId('corner-logo')).toBeVisible();

  await expect(page.getByTestId('slide-frame')).toHaveScreenshot('title-corner-logo.png');
});

test('screenshot: dark bullets with the dark logo', async ({ mount, page }) => {
  await serveLogos(page);
  await mount(slide(look(), { surface: 'dark' }));
  await expect(page.getByTestId('template-logo-dark')).toBeVisible();

  await expect(page.getByTestId('slide-frame')).toHaveScreenshot('dark-bullets-logo.png');
});

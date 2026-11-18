import { test, expect } from '@playwright/experimental-ct-react';
import type { LookView } from '../../../api/types';
import { lookStyle } from '../../../look/lookStyle';
import { lookViewOf } from '../../../test-support/fixtures/lookViewOf';
import { SlideFrame, type SlideFrameProps } from '../../SlideFrame';

test.use({ viewport: { width: 1920, height: 1080 } });

// A 1x1 PNG; the test image needs to load.
const PIXEL =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';
const IMAGE = '/api/templates/tpl_t/assets/art';
const BROKEN = '/api/templates/tpl_t/assets/broken';

const waves: LookView['art'] = { kind: 'generator', generator: 'waves', url: null };

function look(art: LookView['art']) {
  return lookViewOf('talk', 'cobalt', { art });
}

// A plain function, not a component: CT can only mount components from source files.
function slide(view: LookView, props: Partial<Omit<SlideFrameProps, 'children'>> = {}) {
  return (
    <div style={lookStyle(view)}>
      <SlideFrame
        layout="title"
        surface="dark"
        part={null}
        number={1}
        total={16}
        parts={[]}
        deckTitle="Ship smaller"
        author="Sam Rivera"
        look={view}
        deepDive={false}
        titleStamp={null}
        {...props}
      >
        <h1 data-testid="heading" style={{ margin: 0 }}>
          Ship smaller
        </h1>
      </SlideFrame>
    </div>
  );
}

test.beforeEach(async ({ page }) => {
  await page.route('**/api/templates/tpl_t/assets/*', (route) =>
    route.request().url().endsWith('/broken')
      ? route.fulfill({ status: 404 })
      : route.fulfill({
          status: 200,
          contentType: 'image/png',
          body: Buffer.from(PIXEL, 'base64'),
        }),
  );
});

test('shows waves in accent-2 on a title slide', async ({ mount, page }) => {
  const view = look(waves);
  await mount(slide(view));

  const art = page.getByTestId('title-art');
  await expect(art).toBeVisible();
  await expect(art).toHaveAttribute('aria-hidden', 'true');
  await expect(art.locator('path')).toHaveCount(15);
  const [stroke, expected] = await Promise.all([
    art
      .locator('path')
      .first()
      .evaluate((el) => getComputedStyle(el).stroke),
    page.getByTestId('slide-layers').evaluate((el) => {
      const probe = document.createElement('i');
      probe.style.color = 'var(--sw-accent-2)';
      el.appendChild(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return colour;
    }),
  ]);
  expect(stroke).toBe(expected);
  const box = (await art.boundingBox())!;
  expect(box).toMatchObject({ x: 0, y: 0, width: 1920, height: 1080 });
});

test('shows an uploaded image', async ({ mount, page }) => {
  await mount(slide(look({ kind: 'image', generator: null, url: IMAGE })));

  const art = page.getByTestId('title-art');
  await expect(art).toBeVisible();
  await expect(art).toHaveAttribute('src', IMAGE);
  await expect(art).toHaveAttribute('alt', '');
  await expect(art).toHaveCSS('object-fit', 'cover');
  const box = (await art.boundingBox())!;
  expect(box).toMatchObject({ x: 0, y: 0, width: 1920, height: 1080 });
});

test('hides the art on a content slide', async ({ mount, page }) => {
  await mount(slide(look(waves), { layout: 'bullets', surface: 'light', number: 3 }));

  await expect(page.getByTestId('title-art')).toBeHidden();
});

test('renders nothing when the art is none', async ({ mount, page }) => {
  await mount(slide(look({ kind: 'none', generator: null, url: null })));
  await expect(page.getByTestId('title-art')).toHaveCount(0);
});

test('renders nothing for a generator it does not know', async ({ mount, page }) => {
  await mount(slide(look({ kind: 'generator', generator: 'confetti', url: null })));
  await expect(page.getByTestId('title-art')).toHaveCount(0);
});

test('hides a broken image', async ({ mount, page }) => {
  await mount(slide(look({ kind: 'image', generator: null, url: BROKEN })));

  await expect(page.getByTestId('heading')).toBeVisible();
  await expect(page.getByTestId('title-art')).toHaveCount(0);
});

test('keeps the title above the art', async ({ mount, page }) => {
  await mount(slide(look(waves)));

  const heading = page.getByTestId('heading');
  const box = (await heading.boundingBox())!;
  const hit = await page.evaluate(
    ([x, y]) => document.elementFromPoint(x, y)?.getAttribute('data-testid') ?? null,
    [box.x + box.width / 2, box.y + box.height / 2],
  );
  expect(hit).toBe('heading');
});

// Baselines come from CI's playwright job (ct-new-baselines), never this host.
test('screenshot: title slide with waves', async ({ mount, page }) => {
  await mount(slide(look(waves)));
  await expect(page.getByTestId('title-art')).toBeVisible();
  await expect(page).toHaveScreenshot('title-art-waves.png');
});

test('screenshot: title slide with image art', async ({ mount, page }) => {
  await mount(slide(look({ kind: 'image', generator: null, url: IMAGE })));
  await expect(page.getByTestId('title-art')).toBeVisible();
  await expect(page).toHaveScreenshot('title-art-image.png');
});

import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import type { SlideDeck } from '../slots';

// SW-568: the slots a long deck needed. Numbered bullets, a divider photo, a quote portrait, and a link or video on an
// image slide each render from the slot the agent sends. These are DOM assertions; the screenshots stay in each
// layout's own spec.
test.use({ viewport: { width: 1920, height: 1080 } });

const DECK: SlideDeck = {
  title: 'Workshop',
  author: null,
  parts: [],
  look: lookViewOf('workshop', 'cobalt'),
  deepDives: [],
};

function slide(layout: RenderableSlide['layout'], slots: Record<string, unknown>): RenderableSlide {
  return { number: 1, layout, part: null, deepDive: false, slots } as RenderableSlide;
}

async function stubImages(page: Page) {
  await page.route('**/api/images/**', (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="900"/>',
    }),
  );
  // An embed must not reach the network from a component test.
  await page.route(/youtube-nocookie\.com|player\.vimeo\.com/, (route) =>
    route.fulfill({ contentType: 'text/html', body: '<html></html>' }),
  );
}

test('numbered bullets draw 1, 2, 3 and keep the item text', async ({ mount }) => {
  const items = ['Write the run of show', 'Upload the photos', 'Store the deck'];
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('bullets', { headline: 'Three steps', items, style: 'numbered' })}
      total={1}
      step={0}
    />,
  );

  const list = component.getByTestId('bullets-body').locator('ul');
  await expect(list).toHaveAttribute('data-style', 'numbered');
  await expect(component.getByTestId('bullets-item')).toHaveText(items);
  const marker = await list
    .locator('li')
    .nth(1)
    .evaluate((li) => getComputedStyle(li, '::before').content);
  // The number comes from a CSS counter, so the item text stays as the agent sent it.
  expect(marker).toContain('counter(');
});

test('bullets without a style keep the markers', async ({ mount }) => {
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('bullets', { headline: 'Markers', items: ['One', 'Two'] })}
      total={1}
      step={0}
    />,
  );

  const list = component.getByTestId('bullets-body').locator('ul');
  await expect(list).not.toHaveAttribute('data-style');
  const marker = await list
    .locator('li')
    .nth(1)
    .evaluate((li) => getComputedStyle(li, '::before').content);
  expect(marker).not.toContain('counter(');
});

test('a divider photo is drawn full-bleed behind the title', async ({ mount, page }) => {
  await stubImages(page);
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('divider', {
        title: 'Part two',
        image: { image: 'img_1', alt: 'A workshop room' },
      })}
      total={1}
      step={0}
    />,
  );

  const photo = component.getByTestId('divider-image');
  await expect(photo).toHaveAttribute('src', '/api/images/img_1');
  await expect(photo).toHaveAttribute('alt', 'A workshop room');
  const layout = await component.getByTestId('layout-divider').boundingBox();
  const box = await photo.boundingBox();
  expect(box).not.toBeNull();
  expect(layout).not.toBeNull();
  expect(box!.width).toBeGreaterThanOrEqual(layout!.width - 1);
  expect(box!.height).toBeGreaterThanOrEqual(layout!.height - 1);
  // The title is on top: the topmost element at the title's centre is the title (or inside it).
  const onTop = await component.getByTestId('divider-title').evaluate((title) => {
    const r = title.getBoundingClientRect();
    const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
    return hit !== null && title.contains(hit);
  });
  expect(onTop).toBe(true);
});

test('a quote portrait is drawn beside the attribution', async ({ mount, page }) => {
  await stubImages(page);
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('quote', {
        quote: 'Ship smaller, more often.',
        attribution: 'Priya Raman',
        image: { image: 'img_2', alt: 'Priya Raman' },
      })}
      total={1}
      step={0}
    />,
  );

  const attribution = component.getByTestId('quote-attribution');
  const portrait = attribution.getByTestId('quote-portrait');
  await expect(portrait).toHaveAttribute('src', '/api/images/img_2');
  await expect(portrait).toHaveAttribute('alt', 'Priya Raman');
  const who = await attribution.getByTestId('quote-who').boundingBox();
  const face = await portrait.boundingBox();
  expect(face).not.toBeNull();
  expect(who).not.toBeNull();
  // Beside: on the same row, to the left of the name.
  expect(face!.x + face!.width).toBeLessThanOrEqual(who!.x + 1);
  expect(face!.y).toBeLessThan(who!.y + who!.height);
  expect(face!.y + face!.height).toBeGreaterThan(who!.y);
});

const IMAGE = { image: 'img_3', alt: 'The demo' };

test('a YouTube link plays on the image slide with the image as the poster', async ({
  mount,
  page,
}) => {
  await stubImages(page);
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('image', {
        headline: 'The demo',
        image: IMAGE,
        link: 'https://youtu.be/M7lc1UVf-VE',
      })}
      total={1}
      step={0}
    />,
  );

  await expect(component.getByTestId('image-picture')).toHaveAttribute('src', '/api/images/img_3');
  await expect(component.getByTestId('image-video')).toHaveAttribute(
    'src',
    'https://www.youtube-nocookie.com/embed/M7lc1UVf-VE',
  );
  await expect(component.getByTestId('image-link')).toHaveCount(0);
});

test('a Vimeo link plays on the image slide', async ({ mount, page }) => {
  await stubImages(page);
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('image', {
        headline: 'The demo',
        image: IMAGE,
        link: 'https://vimeo.com/76979871',
      })}
      total={1}
      step={0}
    />,
  );

  await expect(component.getByTestId('image-video')).toHaveAttribute(
    'src',
    'https://player.vimeo.com/video/76979871',
  );
});

test('any other link opens from the image in a new tab', async ({ mount, page }) => {
  await stubImages(page);
  const component = await mount(
    <SlideRenderer
      deck={DECK}
      slide={slide('image', {
        headline: 'The story map',
        image: IMAGE,
        link: 'https://example.com/story',
      })}
      total={1}
      step={0}
    />,
  );

  const link = component.getByTestId('image-link');
  await expect(link).toHaveAttribute('href', 'https://example.com/story');
  await expect(link).toHaveAttribute('target', '_blank');
  await expect(link.getByTestId('image-picture')).toHaveCount(1);
  await expect(component.getByTestId('image-video')).toHaveCount(0);
});

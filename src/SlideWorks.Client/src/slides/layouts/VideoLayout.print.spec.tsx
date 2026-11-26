import { test, expect } from '@playwright/experimental-ct-react';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { PrintingProvider } from '../printing';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import { SlideShowingContext } from '../slideShowing';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const ID = 'M7lc1UVf-VE';

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

const slide: RenderableSlide = {
  number: 2,
  layout: 'video',
  part: 1,
  deepDive: false,
  surface: undefined,
  slots: { headline: 'Watch the slice', url: `https://youtu.be/${ID}` },
};

const drawn = (printing: boolean, play?: 'auto') => {
  const content = (
    <SlideShowingContext value>
      <SlideRenderer
        slide={{ ...slide, slots: { ...slide.slots, play } }}
        deck={deck}
        total={3}
        step={0}
      />
    </SlideShowingContext>
  );
  return printing ? <PrintingProvider>{content}</PrintingProvider> : content;
};

test('VideoLayout requests no image from another origin while printing', async ({
  mount,
  page,
}) => {
  const origin = new URL(page.url()).origin;
  const foreign: string[] = [];
  page.on('request', (request) => {
    if (!request.url().startsWith(origin) && !request.url().startsWith('data:'))
      foreign.push(request.url());
  });
  await page.route(
    (url) => url.origin !== origin,
    (route) => route.abort(),
  );

  await mount(drawn(true, 'auto'));

  await expect(page.getByTestId('layout-video')).toBeVisible();
  await expect(page.getByTestId('video-poster')).toHaveCount(0);
  await expect(page.getByTestId('video-frame')).toHaveCount(0);
  await expect(page.getByTestId('video-youtube')).toHaveAttribute('href', /youtube\.com\/watch/);
  expect(foreign).toEqual([]);
});

test('VideoLayout still draws its poster outside printing', async ({ mount, page }) => {
  await page.route(`https://i.ytimg.com/**`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  await mount(drawn(false));

  await expect(page.getByTestId('video-poster')).toBeVisible();
});

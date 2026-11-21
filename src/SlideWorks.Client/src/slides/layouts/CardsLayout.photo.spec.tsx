import { test, expect } from '@playwright/experimental-ct-react';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const DECK: SlideDeck = {
  title: 'Pictures',
  author: null,
  parts: [],
  look: lookViewOf('workshop', 'cobalt'),
  deepDives: [],
};

const SLIDE: RenderableSlide = {
  number: 1,
  layout: 'cards',
  part: null,
  deepDive: false,
  slots: {
    headline: 'Pictures',
    cards: [
      {
        icon: 'coins',
        image: { image: 'img_1', alt: 'A team at a whiteboard' },
        title: 'With a picture',
        text: 'The picture is drawn.',
      },
      { icon: 'coins', title: 'With an icon', text: 'The icon is drawn.' },
    ],
  },
};

test('card picture is shown and the icon is not', async ({ mount, page }) => {
  await page.route('**/api/images/img_1', (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="400" height="300"/>',
    }),
  );
  await mount(<SlideRenderer deck={DECK} slide={SLIDE} total={1} step={0} />);

  const cards = page.getByTestId('cards-card');
  const first = cards.nth(0);
  await expect(first.getByTestId('cards-image')).toHaveAttribute('alt', 'A team at a whiteboard');
  await expect(first.getByTestId('cards-image')).toHaveAttribute('src', '/api/images/img_1');
  await expect(first.getByTestId('cards-icon')).toHaveCount(0);
  await expect(cards.nth(1).getByTestId('cards-icon')).toHaveCount(1);
  await expect(cards.nth(1).getByTestId('cards-image')).toHaveCount(0);
});

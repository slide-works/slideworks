import { test, expect } from '@playwright/experimental-ct-react';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../test-support/fixtures/screens';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const baseLook = lookViewOf('talk', 'cobalt');
const slide = {
  number: 2,
  layout: 'bullets' as const,
  part: null,
  deepDive: false,
  slots: LAYOUT_SLOTS.bullets,
};

function deckWith(joinBar: boolean): SlideDeck {
  return {
    title: 'Ship smaller',
    author: 'Sam Rivera',
    parts: [],
    look: { ...baseLook, joinBar },
    deepDives: [],
  };
}

function mountSlide(joinBar: boolean, live: boolean) {
  return (
    <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live }} owner={false}>
      <SlideRenderer slide={slide} deck={deckWith(joinBar)} total={4} step={0} />
    </JoinProvider>
  );
}

test('the join bar draws a QR code and the code while live with the join bar on', async ({
  mount,
  page,
}) => {
  await mount(mountSlide(true, true));
  const bar = page.getByTestId('join-bar');
  await expect(bar).toBeVisible();
  await expect(page.getByTestId('join-bar-code')).toHaveText(deckPlay.join.codeLabel);
  await expect(page.getByTestId('join-bar-qr').locator('rect')).toHaveAttribute('fill', '#ffffff');
});

test('the join bar stays off when the deck is not live', async ({ mount, page }) => {
  await mount(mountSlide(true, false));
  await expect(page.getByTestId('slide-frame')).toBeVisible();
  await expect(page.getByTestId('join-bar')).toHaveCount(0);
});

test('the join bar stays off when the deck setting is off', async ({ mount, page }) => {
  await mount(mountSlide(false, true));
  await expect(page.getByTestId('slide-frame')).toBeVisible();
  await expect(page.getByTestId('join-bar')).toHaveCount(0);
});

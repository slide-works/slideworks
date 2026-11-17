import { test, expect } from '@playwright/experimental-ct-react';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../test-support/fixtures/screens';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: 'Ship smaller',
  author: 'Sam Rivera',
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};
const join = { ...deckPlay.join, live: true };

function slide(slots: (typeof LAYOUT_SLOTS)['join']) {
  return { number: 1, layout: 'join' as const, part: null, deepDive: false, slots };
}

test('JoinLayout shows the code, the address and the kicker from JoinContext', async ({
  mount,
  page,
}) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-code')).toHaveText(join.codeLabel);
  await expect(page.getByTestId('join-host')).toHaveText(join.hostLabel);
  await expect(page.getByTestId('join-kicker')).toHaveText('Join on your phone');
  await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-dark', 'true');
});

test('JoinLayout shows the count only while live and not hidden', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={{ ...join, live: false }} owner={false}>
      <SlideRenderer slide={slide({})} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('layout-join')).toBeVisible();
  await expect(page.getByTestId('join-count')).toHaveCount(0);
});

test('JoinLayout leaves the count off when the slot hides it', async ({ mount, page }) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <SlideRenderer slide={slide({ count: 'hidden' })} deck={deck} total={1} step={0} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('join-code')).toBeVisible();
  await expect(page.getByTestId('join-count')).toHaveCount(0);
});

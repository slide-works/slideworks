import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { SlideFrame } from '../SlideFrame';
import type { Emphasis } from '../slots/blocks';
import { Emphasised } from './Emphasised';

// Inline emphasis (K10): a phrase of a text in a tone, split out of the text
// as plain text nodes and spans.

const TEXT = 'Small batches ship; big batches slip';

// What the browser reports for a colour, as red, green and blue from 0 to 255.
async function rgbOf(page: Page, testId: string, nth = 0): Promise<number[]> {
  const colour = await page
    .getByTestId(testId)
    .nth(nth)
    .evaluate((element) => getComputedStyle(element).color);
  const numbers = (colour.match(/-?\d*\.?\d+(?:e-?\d+)?/g) ?? []).map(Number);
  // "rgb(r, g, b)" counts to 255; "color(srgb r g b)" counts to 1.
  return numbers.slice(0, 3).map((n) => (colour.startsWith('color(') ? Math.round(n * 255) : n));
}

// The mix the dark surface uses: 55% of the tone with 45% of the night ink.
const lifted = (tone: number[], ink: number[]) =>
  tone.map((channel, i) => Math.round(channel * 0.55 + ink[i] * 0.45));

function framed(surface: 'light' | 'dark', spans: Emphasis[]) {
  return (
    <SlideFrame
      layout={surface === 'dark' ? 'close' : 'rules'}
      surface={surface}
      part={1}
      parts={['One', 'Two']}
      number={2}
      total={9}
      deckTitle="Deck"
      author="Sam Rivera"
      look={lookViewOf('talk', 'cobalt')}
      deepDive={false}
      titleStamp={null}
    >
      <div className="sw-layout">
        <p data-testid="text" style={{ fontSize: 40 }}>
          <Emphasised text={TEXT} spans={spans} />
        </p>
      </div>
    </SlideFrame>
  );
}

test('wraps the phrase once', async ({ mount, page }) => {
  await mount(
    <p data-testid="text">
      <Emphasised text="ship it, then ship it again" spans={[{ text: 'ship', tone: 'accent' }]} />
    </p>,
  );

  const text = page.getByTestId('text');
  await expect(text).toHaveText('ship it, then ship it again');
  const phrases = text.getByTestId('emphasis');
  await expect(phrases).toHaveCount(1);
  await expect(phrases).toHaveText('ship');
  await expect(phrases).toHaveAttribute('data-tone', 'accent');
  // The first place the phrase stands, and nothing else, is inside the span.
  expect(
    await text.evaluate((element) =>
      [...element.childNodes].map((node) => [node.nodeName, node.textContent]),
    ),
  ).toEqual([
    ['SPAN', 'ship'],
    ['#text', ' it, then ship it again'],
  ]);
});

test('markup in the text is shown as text', async ({ mount, page }) => {
  const text = 'Ship <b>small</b> & <img src=x onerror=alert(1)> often';
  await mount(
    <p data-testid="text">
      <Emphasised
        text={text}
        spans={[
          { text: '<b>small</b>', tone: 'positive' },
          { text: '<img src=x onerror=alert(1)>', tone: 'negative' },
        ]}
      />
    </p>,
  );

  const shown = page.getByTestId('text');
  await expect(shown).toHaveText(text);
  await expect(shown.getByTestId('emphasis')).toHaveText([
    '<b>small</b>',
    '<img src=x onerror=alert(1)>',
  ]);
  await expect(shown.locator('b')).toHaveCount(0);
  await expect(shown.locator('img')).toHaveCount(0);
  // Only the two phrase spans are elements; the rest is text.
  await expect(shown.locator('*')).toHaveCount(2);
});

test('two phrases each carry their own tone, in the order of the text', async ({ mount, page }) => {
  await mount(
    <p data-testid="text">
      <Emphasised
        text={TEXT}
        spans={[
          { text: 'slip', tone: 'negative' },
          { text: 'ship', tone: 'positive' },
        ]}
      />
    </p>,
  );
  const phrases = page.getByTestId('text').getByTestId('emphasis');
  await expect(page.getByTestId('text')).toHaveText(TEXT);
  await expect(phrases).toHaveText(['ship', 'slip']);
  await expect(phrases.nth(0)).toHaveAttribute('data-tone', 'positive');
  await expect(phrases.nth(1)).toHaveAttribute('data-tone', 'negative');
});

test('a phrase of pattern characters is found as plain text', async ({ mount, page }) => {
  const phrase = '.* (a+)+$ [x] \\d';
  await mount(
    <p data-testid="text">
      <Emphasised text={`cost ${phrase} more`} spans={[{ text: phrase, tone: 'accent' }]} />
    </p>,
  );
  await expect(page.getByTestId('text')).toHaveText(`cost ${phrase} more`);
  await expect(page.getByTestId('emphasis')).toHaveText(phrase);
});

test('without spans, or with a phrase that is not there, it is the bare text', async ({
  mount,
  page,
}) => {
  await mount(
    <div>
      <p data-testid="none">
        <Emphasised text={TEXT} />
      </p>
      <p data-testid="empty">
        <Emphasised text={TEXT} spans={[]} />
      </p>
      <p data-testid="missing">
        <Emphasised text={TEXT} spans={[{ text: 'medium', tone: 'accent' }]} />
      </p>
    </div>,
  );
  for (const testId of ['none', 'empty', 'missing']) {
    await expect(page.getByTestId(testId)).toHaveText(TEXT);
    await expect(page.getByTestId(testId).locator('*')).toHaveCount(0);
    expect(await page.getByTestId(testId).evaluate((element) => element.childNodes.length)).toBe(1);
  }
});

const TONES: Emphasis[] = [
  { text: 'Small', tone: 'accent' },
  { text: 'ship', tone: 'positive' },
  { text: 'slip', tone: 'negative' },
];
// Cobalt's accent, good and bad, its accent glow and its night ink.
const ACCENT = [47, 91, 234];
const GOOD = [22, 163, 74];
const BAD = [220, 38, 38];
const GLOW = [165, 184, 255];
const NIGHT_INK = [255, 255, 255];

test('on a light slide the tones are the accent, good and bad', async ({ mount, page }) => {
  await mount(framed('light', TONES));
  await expect(page.getByTestId('emphasis')).toHaveText(['Small', 'ship', 'slip']);
  expect(await rgbOf(page, 'emphasis', 0)).toEqual(ACCENT);
  expect(await rgbOf(page, 'emphasis', 1)).toEqual(GOOD);
  expect(await rgbOf(page, 'emphasis', 2)).toEqual(BAD);
  // Only the colour changes: the phrase keeps the text's weight and style.
  await expect(page.getByTestId('emphasis').first()).toHaveCSS('font-style', 'normal');
  const weights = await page
    .getByTestId('text')
    .evaluate((element) => [
      getComputedStyle(element).fontWeight,
      getComputedStyle(element.querySelector('span')!).fontWeight,
    ]);
  expect(weights[1]).toBe(weights[0]);
});

test('on a dark slide the accent is its glow and good and bad are lifted to the night ink', async ({
  mount,
  page,
}) => {
  await mount(framed('dark', TONES));
  await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-surface', 'dark');
  await expect(page.getByTestId('emphasis')).toHaveText(['Small', 'ship', 'slip']);
  expect(await rgbOf(page, 'emphasis', 0)).toEqual(GLOW);
  expect(await rgbOf(page, 'emphasis', 1)).toEqual(lifted(GOOD, NIGHT_INK));
  expect(await rgbOf(page, 'emphasis', 2)).toEqual(lifted(BAD, NIGHT_INK));
});

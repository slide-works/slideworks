import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { deckPrint, sampleDeck } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import type { DeckPrintView } from '../api/types';
import type { Page } from 'playwright/test';

const TOKEN = 'print-token';
const printPath = (id: string) => `/api/render/${id}`;
const route = (id: string, hash = `#token=${TOKEN}`) => `/print/${id}${hash}`;
// The deck's logo is the one image the print fixture draws; serve it as the real page's cookie would.
const stubPrint = async (
  page: Page,
  view: DeckPrintView,
  read: { status?: number; json?: unknown } = { json: view },
) => {
  await page.route(`**/api/images/img_logo0001`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  await stubApi(page, {
    [`${printPath(view.deck.id)}/session`]: { json: {} },
    [`${printPath(view.deck.id)}/print`]: read,
  });
};

test.use({ viewport: { width: 1920, height: 1080 } });

test('PrintPage draws one page per slide in deck order with every step revealed', async ({
  mount,
  page,
}) => {
  const view: DeckPrintView = { ...deckPrint, deck: sampleDeck };
  const stepped = sampleDeck.slides.filter((slide) => slide.steps > 0);
  expect(stepped.length).toBeGreaterThan(0);
  await stubPrint(page, view);

  await mount(<AppRoutes />, { hooksConfig: { route: route(view.deck.id) } });

  const root = page.getByTestId('print-deck');
  await expect(root).toHaveAttribute('data-ready', 'true');
  const sections = page.getByTestId('print-slide');
  await expect(sections).toHaveCount(sampleDeck.slides.length);
  const numbers = await sections.evaluateAll((nodes) =>
    nodes.map((node) => node.getAttribute('data-slide')),
  );
  expect(numbers).toEqual(sampleDeck.slides.map((slide) => String(slide.number)));
  for (const box of await sections.evaluateAll((nodes) =>
    nodes.map((node) => {
      const style = getComputedStyle(node);
      return [
        node.getBoundingClientRect().width,
        node.getBoundingClientRect().height,
        style.breakAfter,
        style.overflow,
      ];
    }),
  ))
    expect(box).toEqual([1920, 1080, 'page', 'hidden']);
  await expect(root.locator('.sw-reveal[data-revealed="false"]')).toHaveCount(0);
  await expect(root.locator('.sw-reveal').first()).toBeAttached();
  await expect(page.getByTestId('player')).toHaveCount(0);
});

test('PrintPage sets data-ready once fonts and images settle', async ({ mount, page }) => {
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  const root = page.getByTestId('print-deck');
  await expect(root).toHaveAttribute('data-ready', 'true');
  await expect(root).not.toHaveAttribute('data-error', /.*/);
  const unsettled = await root.evaluate(
    (element) =>
      Array.from(element.querySelectorAll('img')).filter((image) => !image.complete).length,
  );
  expect(unsettled).toBe(0);
});

test('PrintPage sets data-error and draws the reason when the token is missing or the view is 404', async ({
  mount,
  page,
}) => {
  await stubPrint(page, deckPrint, { status: 404, json: { error: 'Deck not found' } });
  const component = await mount(<AppRoutes />, {
    hooksConfig: { route: route(deckPrint.deck.id, `?token=${TOKEN}`) },
  });
  const root = page.getByTestId('print-deck');
  await expect(root).toHaveAttribute('data-error', 'Render token missing');
  await expect(page.getByTestId('print-error')).toHaveText('Render token missing');
  await component.unmount();

  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });
  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-error', 'Deck not found');
  await expect(page.getByTestId('print-error')).toHaveText('Deck not found');
  await expect(page.getByTestId('print-deck')).not.toHaveAttribute('data-ready', /.*/);
  await expect(page.getByTestId('print-slide')).toHaveCount(0);
});

test('PrintPage removes the token from the address bar', async ({ mount, page }) => {
  await stubPrint(page, deckPrint);
  await page.evaluate(
    (hash) => window.history.replaceState(window.history.state, '', hash),
    `#token=${TOKEN}`,
  );

  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  expect(await page.evaluate(() => window.location.href)).not.toContain(TOKEN);
});

test('PrintPage is ready at once when a word cloud has no words', async ({ mount, page }) => {
  expect(deckPrint.deck.slides.some((slide) => slide.layout === 'word-cloud')).toBe(true);
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  await expect(page.getByTestId('word-cloud-body')).toHaveAttribute('data-packed', 'true');
});

const resultsView = (patch: Partial<DeckPrintView>): DeckPrintView => ({
  ...deckPrint,
  ...patch,
});

test("PrintPage draws a poll slide's bars and percentages from polls[]", async ({
  mount,
  page,
}) => {
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  const slide = page.locator('[data-testid="print-slide"][data-slide="2"]');
  await expect(slide.getByTestId('poll-slide-row-0')).toContainText('Weekly');
  await expect(slide.getByTestId('poll-slide-row-1')).toContainText('Daily');
  await expect(slide.getByTestId('poll-slide-percent-0')).toHaveText('29%');
  await expect(slide.getByTestId('poll-slide-percent-1')).toHaveText('71%');
});

test('PrintPage marks the correct option only when the answer was shown', async ({
  mount,
  page,
}) => {
  const hidden = resultsView({
    polls: deckPrint.polls.map((poll) => ({ ...poll, answerShown: false, correct: null })),
  });
  await stubPrint(page, hidden);
  const component = await mount(<AppRoutes />, { hooksConfig: { route: route(hidden.deck.id) } });
  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  await expect(page.getByTestId('poll-slide-correct-1')).toHaveCount(0);
  await component.unmount();

  await page.unroute('**/api/render/**');
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });
  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  await expect(page.getByTestId('poll-slide-correct-1')).toBeVisible();
  await expect(page.getByTestId('poll-slide-correct-0')).toHaveCount(0);
});

test('PrintPage draws a word cloud from wordClouds[] and is ready only after it packs', async ({
  mount,
  page,
}) => {
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  const body = page.locator('[data-slide="3"]').getByTestId('word-cloud-body');
  await expect(body).toHaveAttribute('data-packed', 'true');
  await expect(body).toContainText('Focused');
  await expect(body).toContainText('Calm');
});

test('PrintPage lists open-text answers from openTexts[] with their numbers', async ({
  mount,
  page,
}) => {
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  const slide = page.locator('[data-slide="4"]');
  await expect(slide.getByTestId('open-text-text')).toHaveText([
    'More demos',
    'A shared definition of done',
  ]);
  await expect(slide.getByTestId('open-text-number')).toHaveText(['1', '3']);
});

test('PrintPage lists the top questions on a Q&A slide from qa', async ({ mount, page }) => {
  await stubPrint(page, deckPrint);
  await mount(<AppRoutes />, { hooksConfig: { route: route(deckPrint.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  const slide = page.locator('[data-slide="5"]');
  await expect(slide.getByTestId('qa-slide-row-text-4')).toHaveText('How do I self-host?');
  await expect(slide.getByTestId('qa-slide-row-votes-4')).toContainText('11');
  await expect(slide.getByTestId('qa-slide-row-text-2')).toHaveText('Can I export to PDF?');
});

test('PrintPage draws an interactive slide with no result entry as it does today', async ({
  mount,
  page,
}) => {
  const bare = resultsView({ polls: [], wordClouds: [], openTexts: [], qa: null });
  await stubPrint(page, bare);
  await mount(<AppRoutes />, { hooksConfig: { route: route(bare.deck.id) } });

  await expect(page.getByTestId('print-deck')).toHaveAttribute('data-ready', 'true');
  await expect(page.getByTestId('poll-slide-percent-0')).toHaveText('');
  await expect(page.getByTestId('open-text-answer')).toHaveCount(0);
  await expect(page.getByTestId('qa-slide-rows')).toHaveCount(0);
  await expect(page.getByTestId('print-slide')).toHaveCount(bare.deck.slides.length);
});

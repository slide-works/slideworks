import { test, expect } from '@playwright/experimental-ct-react';
import type { DeckPageView, LooksView, SlideFinding } from '../../api/types';
import { DeckPageChecksHarness } from '../../test-support/DeckPageChecksHarness';
import { GoTo } from '../../test-support/GoTo';
import {
  PALETTES,
  TEMPLATES,
  looksPaletteOf,
  looksTemplateOf,
} from '../../test-support/fixtures/lookViewOf';
import { deckPage, lookChangeResult } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';

const route = { hooksConfig: { route: `/decks/${deckPage.id}` } };
const deckPath = `/api/decks/${deckPage.id}`;
const looks: LooksView = {
  templates: TEMPLATES.map(looksTemplateOf),
  palettes: PALETTES.map(looksPaletteOf),
};

test.beforeEach(async ({ page }) => {
  await page.route('**/api/looks', (r) => r.fulfill({ json: looks }));
  await page.route(`**${deckPage.look.logo!.url}`, (r) =>
    r.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

const finding = (
  slide: number,
  rule: SlideFinding['rule'],
  severity: SlideFinding['severity'],
  source: SlideFinding['source'],
  message = `${rule} message`,
): SlideFinding => ({ slide, rule, severity, source, where: 'body', message });

const withFindings = (findings: SlideFinding[], revision = deckPage.revision): DeckPageView => ({
  ...deckPage,
  revision,
  findings,
});

const measureCount = (page: import('playwright/test').Page) =>
  page.evaluate(() => window.swMeasureCalls?.length ?? 0);

const badge = (page: import('playwright/test').Page, slide: number) =>
  page
    .getByTestId('deck-thumb')
    .nth(slide - 1)
    .getByTestId('slide-checks-badge');

test('a thumbnail shows the count of errors and warnings', async ({ mount, page }) => {
  const view = withFindings([
    finding(1, 'layoutOutsideTemplate', 'warning', 'stored'),
    finding(1, 'overflow', 'error', 'stored'),
  ]);
  await stubApi(page, { [deckPath]: { json: view } });
  await mount(<DeckPageChecksHarness measured={[]} />, route);

  await expect(badge(page, 1)).toHaveText('2');
  await expect(badge(page, 1)).toHaveAttribute('aria-label', 'Slide 1: 2 problems');
  await expect(badge(page, 2)).toHaveCount(0);
});

test('notes are listed but not counted', async ({ mount, page }) => {
  const view = withFindings([finding(1, 'textOnImage', 'note', 'stored', 'Look at this one.')]);
  await stubApi(page, { [deckPath]: { json: view } });
  await mount(<DeckPageChecksHarness measured={[]} />, route);

  await expect(page.getByTestId('slide-check')).toHaveCount(1);
  await expect(page.getByTestId('slide-check')).toContainText('Look at this one.');
  await expect(badge(page, 1)).toHaveCount(0);
});

test('the selected slide lists each finding as text', async ({ mount, page }) => {
  const view = withFindings([
    finding(1, 'genericAlt', 'warning', 'stored', 'Alt is generic.'),
    finding(1, 'overflow', 'error', 'stored', 'Text runs off.'),
    finding(2, 'smallText', 'warning', 'stored', 'Other slide.'),
  ]);
  await stubApi(page, { [deckPath]: { json: view } });
  await mount(<DeckPageChecksHarness measured={[]} />, route);

  const rows = page.getByTestId('slide-check');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toHaveText('error: Text runs off. (body)');
  await expect(rows.nth(1)).toHaveText('warning: Alt is generic. (body)');
  await page.getByTestId('deck-thumb-select').nth(1).click();
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('Other slide.');
});

test('a finding with markup shows as characters', async ({ mount, page }) => {
  const message = '<img src=x onerror=alert(1)> <b>bold</b>';
  await stubApi(page, {
    [deckPath]: { json: withFindings([finding(1, 'overflow', 'error', 'stored', message)]) },
  });
  await mount(<DeckPageChecksHarness measured={[]} />, route);

  const row = page.getByTestId('slide-check');
  await expect(row).toContainText(message);
  await expect(row.locator('b, img')).toHaveCount(0);
});

test('a slide with no findings says so', async ({ mount, page }) => {
  await stubApi(page, { [deckPath]: { json: withFindings([]) } });
  await mount(<DeckPageChecksHarness measured={[]} />, route);

  await expect(page.getByTestId('slide-checks-none')).toHaveText(
    'No problems found on this slide.',
  );
  await expect(page.getByRole('heading', { name: 'Checks' })).toBeVisible();
});

test('measured and stored findings for one slide are merged', async ({ mount, page }) => {
  const view = withFindings([finding(1, 'genericAlt', 'warning', 'stored', 'Stored one.')]);
  await stubApi(page, { [deckPath]: { json: view } });
  await mount(
    <DeckPageChecksHarness
      measured={[finding(1, 'overflow', 'error', 'measured', 'Measured one.')]}
    />,
    route,
  );

  const rows = page.getByTestId('slide-check');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('Measured one.');
  await expect(rows.nth(1)).toContainText('Stored one.');
  await expect(badge(page, 1)).toHaveText('2');
});

test('a new revision checks again', async ({ mount, page }) => {
  const hub = await stubDeckHub(page);
  let reads = 0;
  await stubApi(page, {});
  await page.route(`**${deckPath}`, (r) => {
    reads += 1;
    return r.fulfill({ json: reads <= 2 ? withFindings([]) : withFindings([], 99) });
  });
  await mount(<DeckPageChecksHarness measured={[]} />, route);
  await expect.poll(() => hub.joined).toEqual([deckPage.id]);
  await expect.poll(() => reads).toBe(2);
  // The first pass has measured every slide before the deck is told about a change.
  await expect.poll(() => measureCount(page)).toBe(deckPage.slides.length);
  const settled = deckPage.slides.length;

  hub.push({ type: 'deckChanged', deckId: deckPage.id });
  await expect.poll(() => measureCount(page)).toBeGreaterThan(settled);
});

test('a changed look measures again', async ({ mount, page }) => {
  await stubApi(page, { [deckPath]: { json: withFindings([]) } });
  await page.route(`**/api/decks/${deckPage.id}/look`, (r) =>
    r.fulfill({ json: lookChangeResult }),
  );
  await mount(<DeckPageChecksHarness measured={[]} />, route);
  await expect.poll(() => measureCount(page)).toBe(deckPage.slides.length);
  const before = await measureCount(page);

  // No revision change: the look alone is saved, as showLook shows it.
  await page.getByTestId('look-template').selectOption('workshop');

  await expect.poll(() => measureCount(page)).toBeGreaterThan(before);
});

test('a slide that cannot be measured says so and the others keep their findings', async ({
  mount,
  page,
}) => {
  await stubApi(page, { [deckPath]: { json: withFindings([]) } });
  await mount(
    <DeckPageChecksHarness
      measured={[finding(1, 'overflow', 'error', 'measured', 'Measured one.')]}
      throwOn={2}
    />,
    route,
  );

  await expect(badge(page, 1)).toHaveText('1');
  await page.getByTestId('deck-thumb').nth(1).click();
  await expect(page.getByTestId('slide-checks-unchecked')).toHaveText('Could not check slide 2.');
  await page.getByTestId('deck-thumb').nth(0).click();
  await expect(page.getByTestId('slide-check')).toHaveCount(1);
});

test("opening another deck drops the first deck's findings", async ({ mount, page }) => {
  const other: DeckPageView = { ...deckPage, id: 'Zz9OtherDeck', findings: [] };
  await stubApi(page, {
    [deckPath]: {
      json: withFindings([finding(1, 'overflow', 'error', 'stored', 'First deck only.')]),
    },
    '/api/decks/Zz9OtherDeck': { json: other },
  });
  await mount(
    <GoTo>
      <DeckPageChecksHarness measured={[]} />
    </GoTo>,
    route,
  );
  await expect(page.getByTestId('slide-check')).toHaveCount(1);

  await page.getByTestId('goto-path').fill('/decks/Zz9OtherDeck');
  await page.getByTestId('goto').click();
  await expect(page.getByTestId('slide-checks-none')).toBeVisible();
  await expect(page.getByTestId('slide-check')).toHaveCount(0);
});

test('screenshot: deck page with checks', async ({ mount, page }) => {
  await stubApi(page, {
    [deckPath]: {
      json: withFindings([
        finding(1, 'overflow', 'error', 'stored', 'Text runs off the slide.'),
        finding(2, 'genericAlt', 'warning', 'stored', 'Alt is generic.'),
      ]),
    },
  });
  await mount(<DeckPageChecksHarness measured={[]} />, route);
  await expect(badge(page, 1)).toBeVisible();
  await expect(page).toHaveScreenshot('deck-page-with-checks.png');
});

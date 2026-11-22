import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../AppRoutes';
import { me } from '../test-support/fixtures/screens';
import { layoutReview, templateEditor } from '../test-support/fixtures/templates';
import { stubApi } from '../test-support/stubApi';
import { stubTemplateAssets } from '../test-support/stubTemplateAssets';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { StandInReview } from './LayoutReviewPage.fixtures';
import { REVIEW_TEST_IDS, reviewRenderTestId } from './review/testIds';
import { TEMPLATES_TEST_IDS } from './testIds';
import type { LayoutReviewView } from './types';

const route = {
  hooksConfig: { route: `/templates/${templateEditor.id}/layouts/${layoutReview.name}` },
};
const approve = (page: Page) => page.getByRole('button', { name: 'Approve layout bar-chart' });
const status = (page: Page) => page.getByTestId(REVIEW_TEST_IDS.status);
const withSurface = (surface: LayoutReviewView['surface']): LayoutReviewView => ({
  ...layoutReview,
  surface,
});

async function stub(page: Page, responses: Parameters<typeof stubTemplatesApi>[1] = {}) {
  await stubApi(page, { '/api/me': { json: me } });
  return stubTemplatesApi(page, responses);
}

test('shows sample and maximum renders', async ({ mount, page }) => {
  await stub(page);
  await mount(<AppRoutes />, route);

  await expect(page.getByRole('heading', { name: 'bar-chart', level: 1 })).toBeVisible();
  await expect(status(page)).toHaveText('Needs review');
  for (const render of ['sample', 'steps', 'maximum'] as const) {
    await expect(
      page.getByTestId(reviewRenderTestId(render)).getByTestId('custom-layout'),
    ).toBeVisible();
  }
  await expect(page.getByTestId(REVIEW_TEST_IDS.safeAreaBox)).toHaveCount(1);
  await expect(page.getByTestId(reviewRenderTestId('dark'))).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Slots' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Checked tree' })).toBeVisible();
});

test('approve is off while the maximum leaves the safe area', async ({ mount, page }) => {
  await stub(page);
  await mount(
    <StandInReview
      overflow={['bar at x 200–1850, y 300–400', 'note at x 90–400, y 900–950']}
      contrast={[]}
    />,
    route,
  );

  await expect(page.getByText('2 items leave the safe area')).toBeVisible();
  await expect(page.getByText('bar at x 200–1850, y 300–400')).toBeVisible();
  await expect(page.getByText('note at x 90–400, y 900–950')).toBeVisible();
  await expect(approve(page)).toBeDisabled();
});

test('approve is off while contrast is low', async ({ mount, page }) => {
  await stub(page);
  await mount(<StandInReview overflow={[]} contrast={['Quiet note 3.2:1, needs 4.5']} />, route);

  await expect(page.getByText('1 text is below the contrast bar')).toBeVisible();
  await expect(page.getByText('Quiet note 3.2:1, needs 4.5')).toBeVisible();
  await expect(approve(page)).toBeDisabled();
});

// The contract's maximum is the server's worst case (every text slot filled to its limit with wide
// letters), which this sample layout does not fit: the real measurement must say so. A layout that
// fits passes: here its "maximum" is the sample's own content.
const fitting: LayoutReviewView = { ...layoutReview, maximum: layoutReview.sample };

test('the real measurement passes the sample layout', async ({ mount, page }) => {
  await stub(page, { layoutReview: { json: fitting } });
  await mount(<AppRoutes />, route);

  await expect(approve(page)).toBeEnabled();
  await expect(page.getByTestId(REVIEW_TEST_IDS.checks)).toContainText('Safe areaPassed');
  await expect(page.getByTestId(REVIEW_TEST_IDS.checks)).toContainText('Contrast on lightPassed');
});

test('the real measurement flags a maximum that leaves the safe area', async ({ mount, page }) => {
  await stub(page);
  await mount(<AppRoutes />, route);

  await expect(page.getByText(/items? leaves? the safe area/)).toBeVisible();
  await expect(approve(page)).toBeDisabled();
});

test('approve sends the asset and the measured lists', async ({ mount, page }) => {
  const requests = await stub(page);
  await mount(<StandInReview overflow={[]} contrast={[]} />, route);

  await approve(page).click();
  await expect(status(page)).toHaveText('Needs review');
  await expect
    .poll(() => requests.filter(({ call }) => call === 'approveLayout'))
    .toEqual([
      {
        call: 'approveLayout',
        id: templateEditor.id,
        body: { asset: layoutReview.asset, overflow: [], contrast: [] },
      },
    ]);
});

// The approval body is the two measured lists, and the gate keeps any finding out of it: with a
// finding on either surface Approve is off and a forced click sends nothing, so findings never
// reach the server through a path that skips the gate.
test('findings keep approval off and nothing is sent', async ({ mount, page }) => {
  const requests = await stub(page, { layoutReview: { json: withSurface('either') } });
  await mount(
    <StandInReview
      overflow={['bar at x 200–1850, y 300–400']}
      contrast={['Quiet 3.2:1, needs 4.5']}
    />,
    route,
  );

  await expect(page.getByText('Too faint on dark')).toBeVisible();
  await expect(approve(page)).toBeDisabled();
  await approve(page)
    .click({ force: true, timeout: 1000 })
    .catch(() => undefined);
  expect(requests.filter(({ call }) => call === 'approveLayout')).toEqual([]);
});

test('a render that shows the error state cannot be approved', async ({ mount, page }) => {
  const broken = { ...fitting, sample: { ...fitting.sample, headline: { not: 'text' } } };
  await stub(page, { layoutReview: { json: broken } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('custom-layout-error').first()).toBeVisible();
  await expect(page.getByText(/render shows the layout's error state/)).toBeVisible();
  await expect(page.getByText(/renders? (was|were) not measured/)).toBeVisible();
  await expect(page.getByTestId(REVIEW_TEST_IDS.checks)).toContainText('Renders drawnFailed');
  await expect(approve(page)).toBeDisabled();
});

test('a layout the template does not hold cannot be approved', async ({ mount, page }) => {
  const missing = { ...fitting, layout: { ...fitting.layout, name: 'other' } };
  await stub(page, { layoutReview: { json: missing } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('unknown-layout').first()).toBeVisible();
  await expect(page.getByText(/renders were not measured/)).toBeVisible();
  await expect(approve(page)).toBeDisabled();
});

test('approve turns the status to Approved and moves focus to it', async ({ mount, page }) => {
  const requests = await stub(page, {
    layoutReview: [{ json: layoutReview }, { json: { ...layoutReview, approved: true } }],
  });
  await mount(<StandInReview overflow={[]} contrast={[]} />, route);

  await approve(page).click();
  await expect(status(page)).toHaveText('Approved');
  await expect(status(page)).toBeFocused();
  await expect(approve(page)).toBeDisabled();
  // Approving answers 204, so the review was read again.
  expect(requests.filter(({ call }) => call === 'layoutReview')).toHaveLength(2);
});

test('a failed approval shows a message and keeps focus', async ({ mount, page }) => {
  await stub(page, { approveLayout: { status: 500 } });
  await mount(<StandInReview overflow={[]} contrast={[]} />, route);

  await approve(page).focus();
  await approve(page).click();
  await expect(page.getByText('Could not approve bar-chart. Try again.')).toBeVisible();
  await expect(approve(page)).toBeFocused();
  await expect(status(page)).toHaveText('Needs review');
});

test('a changed layout says to reload', async ({ mount, page }) => {
  await stub(page, {
    approveLayout: {
      status: 409,
      json: { error: 'layout_changed', message: 'The layout changed.' },
    },
  });
  await mount(<StandInReview overflow={[]} contrast={[]} />, route);

  await approve(page).click();
  await expect(
    page.getByText('This layout changed. Reload to review the new files.'),
  ).toBeVisible();
  await expect(approve(page)).toBeFocused();
});

test('a dark render shows only when the surface allows dark', async ({ mount, page }) => {
  await stub(page, { layoutReview: { json: withSurface('either') } });
  await mount(<AppRoutes />, route);
  await expect(
    page.getByTestId(reviewRenderTestId('dark')).getByTestId('custom-layout'),
  ).toBeVisible();
  await expect(page.getByText('Contrast on dark')).toBeVisible();
});

// Chromium serialises a computed color-mix (--sw-chip on the dark surface) as color(srgb …): text
// on a chip must be measured, not reported as an unreadable colour.
test('the dark render of text on a chip measures and can be approved', async ({ mount, page }) => {
  const onChip: LayoutReviewView = {
    ...fitting,
    surface: 'either',
    layout: {
      ...fitting.layout,
      css: `${fitting.layout.css}\n.cl-0a1b2c3d-bar-chart__headline { background: var(--sw-chip); }`,
    },
  };
  await stub(page, { layoutReview: { json: onChip } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId(REVIEW_TEST_IDS.checks)).toContainText('Contrast on darkPassed');
  await expect(page.getByText(/could not be read/)).toHaveCount(0);
  await expect(approve(page)).toBeEnabled();
});

test('no dark render for a light layout', async ({ mount, page }) => {
  await stub(page);
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId(reviewRenderTestId('sample'))).toBeVisible();
  await expect(page.getByTestId(reviewRenderTestId('dark'))).toHaveCount(0);
  await expect(page.getByText('Contrast on dark')).toHaveCount(0);
});

test('a layout that fails to load offers Retry', async ({ mount, page }) => {
  await stub(page, { layoutReview: [{ status: 500 }, { json: layoutReview }] });
  await mount(<AppRoutes />, route);

  await expect(page.getByText('Could not load this layout.')).toBeVisible();
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(page.getByRole('heading', { name: 'bar-chart', level: 1 })).toBeVisible();
});

test('a layout that is not the caller’s is not found', async ({ mount, page }) => {
  await stub(page, { layoutReview: { status: 404 } });
  await mount(<AppRoutes />, route);

  await expect(
    page.getByText('This layout does not exist or is not yours to review.'),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Retry' })).toHaveCount(0);
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.layoutReviewPage)).toBeVisible();
});

test('a failed re-read after a replacement offers Retry that measures again', async ({
  mount,
  page,
}) => {
  await stub(page, {
    layoutReview: [{ json: fitting }, { status: 500 }, { json: fitting }],
  });
  await stubTemplateAssets(page, templateEditor.id);
  await mount(<AppRoutes />, route);
  await expect(approve(page)).toBeEnabled();

  await page.getByTestId('layout-files').setInputFiles([
    { name: 'l.html', mimeType: 'text/html', buffer: Buffer.from('<div class="chart"></div>') },
    { name: 'l.css', mimeType: 'text/css', buffer: Buffer.from('.chart { color: red; }') },
    {
      name: 'l.json',
      mimeType: 'application/json',
      buffer: Buffer.from(JSON.stringify({ name: 'bar-chart', surface: 'light', slots: {} })),
    },
  ]);
  await expect(page.getByText('Could not load this layout.')).toBeVisible();
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(approve(page)).toBeEnabled();
});

const replacementFiles = [
  { name: 'l.html', mimeType: 'text/html', buffer: Buffer.from('<div class="chart"></div>') },
  { name: 'l.css', mimeType: 'text/css', buffer: Buffer.from('.chart { color: red; }') },
  {
    name: 'l.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({ name: 'bar-chart', surface: 'light', slots: {} })),
  },
];

// Assets are content-addressed: the same files give the same asset id, and the page must still
// measure and offer Approve.
test('replacing a layout with files that give the same asset keeps Approve available', async ({
  mount,
  page,
}) => {
  const requests = await stub(page, { layoutReview: { json: fitting } });
  await stubTemplateAssets(page, templateEditor.id);
  await mount(<AppRoutes />, route);
  await expect(approve(page)).toBeEnabled();

  await page.getByTestId('layout-files').setInputFiles(replacementFiles);
  await expect.poll(() => requests.filter(({ call }) => call === 'layoutReview').length).toBe(2);
  await expect(page.getByText('Measuring the layout…')).toHaveCount(0);
  await expect(approve(page)).toBeEnabled();
});

test('a failed re-read after approval offers Retry', async ({ mount, page }) => {
  await stub(page, { layoutReview: [{ json: fitting }, { status: 500 }, { json: fitting }] });
  await mount(<AppRoutes />, route);
  await expect(approve(page)).toBeEnabled();

  await approve(page).click();
  await expect(page.getByText('Could not load this layout.')).toBeVisible();
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(approve(page)).toBeEnabled();
});

test('screenshot: layout review, needs review', async ({ mount, page }) => {
  await stub(page, { layoutReview: { json: fitting } });
  await mount(<AppRoutes />, route);
  await expect(approve(page)).toBeEnabled();
  await expect(page).toHaveScreenshot('layout-review-needs-review.png', { fullPage: true });
});

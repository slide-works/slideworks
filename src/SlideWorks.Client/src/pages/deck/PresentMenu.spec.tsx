import { test, expect } from '@playwright/experimental-ct-react';
import { LocationProbe } from '../../test-support/LocationProbe';
import { deckPage } from '../../test-support/fixtures/screens';
import { PresentMenu } from './PresentMenu';

const owned = { ...deckPage, isOwner: true };

test('owner, the arrow opens the three ways to present', async ({ mount, page }) => {
  await mount(<PresentMenu view={owned} />);
  await expect(page.getByTestId('present-menu-presenter-view')).toHaveCount(0);

  await page.getByTestId('present-menu').click();
  await expect(page.getByTestId('present-menu-player')).toBeFocused();
  await expect(page.getByTestId('present-menu-presenter-view')).toBeVisible();
  await expect(page.getByTestId('present-menu-phone')).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(page.getByTestId('present-menu-phone')).toHaveCount(0);
  await expect(page.getByTestId('present-menu')).toBeFocused();
});

test('owner, Present still opens the player', async ({ mount, page }) => {
  await mount(
    <LocationProbe>
      <PresentMenu view={owned} />
    </LocationProbe>,
  );
  await page.getByTestId('deck-present').click();
  await expect(page.getByTestId('location')).toHaveText(deckPage.playUrl);
});

test('not the owner, there is no menu, only Present', async ({ mount, page }) => {
  await mount(<PresentMenu view={{ ...deckPage, isOwner: false, canEdit: false }} />);
  await expect(page.getByTestId('deck-present')).toBeVisible();
  await expect(page.getByTestId('present-menu')).toHaveCount(0);
});

test('a writer, the arrow opens the three ways to present', async ({ mount, page }) => {
  await mount(
    <PresentMenu view={{ ...deckPage, isOwner: false, canEdit: true, access: 'write' }} />,
  );
  await page.getByTestId('present-menu').click();
  await expect(page.getByTestId('present-menu-player')).toBeVisible();
  await expect(page.getByTestId('present-menu-presenter-view')).toBeVisible();
  await expect(page.getByTestId('present-menu-phone')).toBeVisible();
});

test('Presenter view opens the audience window and goes to the presenter page', async ({
  mount,
  page,
}) => {
  await mount(
    <LocationProbe>
      <PresentMenu view={owned} />
    </LocationProbe>,
  );
  await page.evaluate(() => {
    const opened: string[][] = [];
    (window as unknown as { opened: string[][] }).opened = opened;
    window.open = (url, target) => {
      opened.push([String(url), String(target)]);
      return null;
    };
  });

  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-presenter-view').click();

  await expect(page.getByTestId('location')).toHaveText(`/decks/${owned.id}/present`);
  const opened = await page.evaluate(() => (window as unknown as { opened: string[][] }).opened);
  expect(opened).toEqual([[`/d/${owned.id}/${owned.slug}?present`, `sw-audience-${owned.id}`]]);
});

test('Control from your phone opens the dialog with the remote url and no phone chips', async ({
  mount,
  page,
}) => {
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-phone').click();

  await expect(page.getByTestId('phone-connect')).toBeVisible();
  await expect(page.getByTestId('phone-connect-url')).toHaveText(
    `${new URL(page.url()).origin}/remote?deck=${owned.id}`,
  );
  await expect(page.getByTestId('phone-connect-chip')).toHaveCount(0);

  await page.keyboard.press('Escape');
  await expect(page.getByTestId('phone-connect')).toHaveCount(0);
  await expect(page.getByTestId('present-menu')).toBeFocused();
});

test('Present menu offers Download PDF to someone who can edit', async ({ mount, page }) => {
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  const item = page.getByTestId('present-menu-pdf');
  await expect(item).toHaveAttribute('role', 'menuitem');
  await expect(item).toHaveAttribute('aria-label', `Download PDF of ${owned.title}`);
  await expect(item).toContainText('Download PDF');
  await expect(item).toContainText(
    "One page per slide, every reveal shown, with the last talk's results.",
  );
});

test('Present menu has no Download PDF for a viewer', async ({ mount, page }) => {
  await mount(<PresentMenu view={{ ...deckPage, isOwner: false, canEdit: false }} />);
  await expect(page.getByTestId('present-menu')).toHaveCount(0);
  await expect(page.getByTestId('present-menu-pdf')).toHaveCount(0);
});

test('Download PDF saves <slug>.pdf and closes the menu', async ({ mount, page }) => {
  await page.route(`**/api/decks/${owned.id}/pdf`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/pdf',
      headers: { 'Content-Disposition': `attachment; filename="${owned.slug}.pdf"` },
      body: '%PDF-1.4',
    }),
  );
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();

  const download = page.waitForEvent('download');
  await page.getByTestId('present-menu-pdf').click();

  expect((await download).suggestedFilename()).toBe(`${owned.slug}.pdf`);
  await expect(page.getByTestId('present-menu-pdf')).toHaveCount(0);
  await expect(page.getByTestId('present-menu')).toBeFocused();
});

test('Download PDF shows Making the PDF… while it runs', async ({ mount, page }) => {
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let requests = 0;
  await page.route(`**/api/decks/${owned.id}/pdf`, async (route) => {
    requests += 1;
    await gate;
    await route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.4' });
  });
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  const item = page.getByTestId('present-menu-pdf');

  await item.click();
  await expect(item).toContainText('Making the PDF…');
  await expect(item).toHaveAttribute('aria-disabled', 'true');
  await expect(item).toBeFocused();
  await item.click({ force: true });
  release();

  await expect(page.getByTestId('present-menu-pdf')).toHaveCount(0);
  expect(requests).toBe(1);
});

test("Download PDF shows the server's error and keeps focus on the item", async ({
  mount,
  page,
}) => {
  await page.route(`**/api/decks/${owned.id}/pdf`, (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'The PDF renderer is busy. Try again in a minute.' }),
    }),
  );
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-pdf').click();

  await expect(page.getByTestId('present-menu-pdf-error')).toHaveText(
    'The PDF renderer is busy. Try again in a minute.',
  );
  await expect(page.getByTestId('present-menu-pdf-error')).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId('present-menu-pdf')).toBeFocused();
  await expect(page.getByTestId('present-menu-pdf')).toHaveAttribute('aria-disabled', 'false');
});

test('Download PDF keeps the menu open on Escape and shows the 503 when it lands', async ({
  mount,
  page,
}) => {
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(`**/api/decks/${owned.id}/pdf`, async (route) => {
    await gate;
    await route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'The PDF renderer is busy. Try again in a minute.' }),
    });
  });
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-pdf').click();
  await expect(page.getByTestId('present-menu-pdf')).toContainText('Making the PDF…');

  await page.keyboard.press('Escape');
  await expect(page.getByTestId('present-menu-pdf')).toBeVisible();
  release();

  await expect(page.getByTestId('present-menu-pdf-error')).toHaveText(
    'The PDF renderer is busy. Try again in a minute.',
  );
  await expect(page.getByTestId('present-menu-pdf')).toBeFocused();
});

test('Download PDF keeps the menu open on an arrow click and shows the 503 when it lands', async ({
  mount,
  page,
}) => {
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(`**/api/decks/${owned.id}/pdf`, async (route) => {
    await gate;
    await route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'The PDF renderer is busy. Try again in a minute.' }),
    });
  });
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-pdf').click();
  await expect(page.getByTestId('present-menu-pdf')).toContainText('Making the PDF…');

  await page.getByTestId('present-menu').click();
  await expect(page.getByTestId('present-menu-pdf')).toBeVisible();
  release();

  await expect(page.getByTestId('present-menu-pdf-error')).toBeVisible();
  await expect(page.getByTestId('present-menu-pdf-error')).toHaveText(
    'The PDF renderer is busy. Try again in a minute.',
  );
});

test('Download PDF announces Making the PDF… in a status region', async ({ mount, page }) => {
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(`**/api/decks/${owned.id}/pdf`, async (route) => {
    await gate;
    await route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.4' });
  });
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-pdf').click();

  await expect(page.getByRole('status')).toHaveText('Making the PDF…');
  release();
  await expect(page.getByTestId('present-menu-pdf')).toHaveCount(0);
});

test('Download PDF shows the client text when the network fails', async ({ mount, page }) => {
  await page.route(`**/api/decks/${owned.id}/pdf`, (route) => route.abort('failed'));
  await mount(<PresentMenu view={owned} />);
  await page.getByTestId('present-menu').click();
  await page.getByTestId('present-menu-pdf').click();

  await expect(page.getByTestId('present-menu-pdf-error')).toHaveText(
    "Couldn't make the PDF. Try again.",
  );
  await expect(page.getByTestId('present-menu-pdf')).toBeFocused();
});

test('menu screenshot', async ({ mount, page }) => {
  await mount(
    <div className="flex justify-end p-4" style={{ width: 420, height: 260 }}>
      <PresentMenu view={owned} />
    </div>,
  );
  await page.getByTestId('present-menu').click();
  await expect(page).toHaveScreenshot('present-menu.png');
});

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
  await mount(<PresentMenu view={{ ...deckPage, isOwner: false }} />);
  await expect(page.getByTestId('deck-present')).toBeVisible();
  await expect(page.getByTestId('present-menu')).toHaveCount(0);
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

test('menu screenshot', async ({ mount, page }) => {
  await mount(
    <div className="flex justify-end p-4" style={{ width: 420, height: 260 }}>
      <PresentMenu view={owned} />
    </div>,
  );
  await page.getByTestId('present-menu').click();
  await expect(page).toHaveScreenshot('present-menu.png');
});

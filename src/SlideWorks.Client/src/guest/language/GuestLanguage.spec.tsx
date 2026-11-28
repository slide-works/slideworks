import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { audienceState, guestView, translationsOff } from '../../test-support/fixtures/guestStates';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { GUEST_TEST_IDS } from '../testIds';

// The live sample deck offers en (primary), es, th and ja. The page starts in English with two lines still untranslated in
// Thai, the state SetLanguage answers with.
const english = audienceState({ language: { code: 'en', fallbackLines: 0 } });
const thai = audienceState({ language: { code: 'th', fallbackLines: 2 } });
const view = { ...guestView('live'), state: english };

async function mountGuest(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Page,
  guest = view,
) {
  const hub = await stubDeckHub(page, { audience: { state: guest.state } });
  hub.setLanguageReply(thai);
  await stubApi(page, { '/api/join/482913': { json: guest } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toBeVisible();
  return hub;
}

const chip = (page: Page) => page.getByTestId(GUEST_TEST_IDS.languageChip);

test.describe('phone in Thai', () => {
  test.use({ locale: 'th-TH' });

  test('GuestLanguage_WhenPhoneLanguageOffered_ShouldStartInItWithBanner', async ({
    mount,
    page,
  }) => {
    const hub = await mountGuest(mount, page);
    await expect(chip(page)).toHaveText('ไทย');
    await expect(chip(page)).toHaveAccessibleName('Read the slides in Thai');
    await expect(page.getByTestId(GUEST_TEST_IDS.languageBanner)).toHaveText(
      /Showing the slides in ไทย\./,
    );
    expect(hub.languageSets).toEqual([{ code: '482913', language: 'th' }]);
  });

  test('GuestLanguage_WhenReconnected_ShouldSetLanguageAgain', async ({ mount, page }) => {
    const hub = await mountGuest(mount, page);
    await expect.poll(() => hub.languageSets.length).toBe(1);
    hub.drop();
    await expect.poll(() => hub.languageSets.length, { timeout: 15_000 }).toBe(2);
    expect(hub.audienceJoins).toHaveLength(2);
    expect(hub.languageSets[1]).toEqual({ code: '482913', language: 'th' });
  });
});

test.describe('phone in French', () => {
  test.use({ locale: 'fr-FR' });

  test('GuestLanguage_WhenPhoneLanguageNotOffered_ShouldStartInPrimaryWithoutBanner', async ({
    mount,
    page,
  }) => {
    const hub = await mountGuest(mount, page);
    await expect(chip(page)).toHaveText('EN');
    await expect(page.getByTestId(GUEST_TEST_IDS.languageBanner)).toHaveCount(0);
    expect(hub.languageSets).toEqual([]);
  });
});

test('GuestLanguage_WhenOptionPicked_ShouldCallSetLanguageAndRemember', async ({ mount, page }) => {
  const hub = await mountGuest(mount, page);
  await chip(page).click();
  const sheet = page.getByTestId(GUEST_TEST_IDS.languageSheet);
  await expect(sheet.getByRole('heading', { name: 'Read the slides in' })).toBeVisible();
  const options = sheet.getByTestId(GUEST_TEST_IDS.languageOption);
  await expect(options).toHaveCount(4);
  await expect(options.nth(2)).toContainText('ไทย');
  await expect(options.nth(2)).toContainText('Thai');
  await expect(options.nth(2)).toContainText('83% translated');
  await expect(options.nth(0)).toContainText('Complete');

  await options.nth(2).click();

  await expect(sheet).toHaveCount(0);
  await expect(chip(page)).toHaveText('ไทย');
  expect(hub.languageSets).toEqual([{ code: '482913', language: 'th' }]);
  expect(await page.evaluate(() => sessionStorage.getItem('sw-guest-language:482913'))).toBe('th');
});

test('GuestLanguage_WhenTranslationsOff_ShouldShowNoChip', async ({ mount, page }) => {
  await mountGuest(mount, page, translationsOff(view));
  await expect(chip(page)).toHaveCount(0);
  await expect(page.getByTestId(GUEST_TEST_IDS.fallbackNote)).toHaveCount(0);
});

test('GuestLanguage_WhenFallbackLines_ShouldShowNote', async ({ mount, page }) => {
  await mountGuest(mount, page, guestView('live'));
  await expect(page.getByTestId(GUEST_TEST_IDS.fallbackNote)).toHaveText(
    '2 lines on this slide are in English',
  );
});

test('GuestLanguage_WhenOneFallbackLine_ShouldShowSingularNote', async ({ mount, page }) => {
  const one = guestView('live', { language: { code: 'th', fallbackLines: 1 } });
  await mountGuest(mount, page, one);
  await expect(page.getByTestId(GUEST_TEST_IDS.fallbackNote)).toHaveText(
    '1 line on this slide is in English',
  );
});

test('GuestLanguage_WhenNoFallbackLines_ShouldHideNote', async ({ mount, page }) => {
  await mountGuest(mount, page);
  await expect(page.getByTestId(GUEST_TEST_IDS.fallbackNote)).toHaveCount(0);
});

test('GuestLanguage_WhenSetLanguageFails_ShouldShowMessageAndStay', async ({ mount, page }) => {
  const hub = await mountGuest(mount, page);
  hub.fail('SetLanguage', 'not_joined');
  await chip(page).click();
  await page.getByTestId(GUEST_TEST_IDS.languageOption).nth(2).click();

  await expect(page.getByTestId(GUEST_TEST_IDS.languageError)).toHaveText(
    "Couldn't switch language. Try again.",
  );
  await expect(page.getByTestId(GUEST_TEST_IDS.languageSheet)).toBeVisible();
  await expect(chip(page)).toHaveText('EN');
  expect(await page.evaluate(() => sessionStorage.getItem('sw-guest-language:482913'))).toBeNull();
});

test('GuestLanguage_WhenEscapePressed_ShouldCloseSheetAndFocusChip', async ({ mount, page }) => {
  await mountGuest(mount, page);
  await chip(page).click();
  await expect(page.getByTestId(GUEST_TEST_IDS.languageSheet)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(GUEST_TEST_IDS.languageSheet)).toHaveCount(0);
  await expect(chip(page)).toBeFocused();
});

test.describe('phone in Thai, banner', () => {
  test.use({ locale: 'th-TH' });

  test('GuestLanguage_WhenBannerChangePressed_ShouldOpenSheetAndHideBanner', async ({
    mount,
    page,
  }) => {
    await mountGuest(mount, page);
    await page.getByTestId(GUEST_TEST_IDS.languageBannerChange).click();
    await expect(page.getByTestId(GUEST_TEST_IDS.languageSheet)).toBeVisible();
    await expect(page.getByTestId(GUEST_TEST_IDS.languageBanner)).toHaveCount(0);
  });
});

test('GuestLanguage_WhenSideways_ShouldRenderOnlyOneLanguageChip', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mountGuest(mount, page);
  await expect(chip(page)).toHaveCount(1);
  await page.setViewportSize({ width: 844, height: 390 });
  await expect(chip(page)).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(chip(page)).toHaveCount(1);
});

test.describe('screenshots', () => {
  test.use({ locale: 'th-TH' });

  test('GuestLanguage screenshot of the banner and note', async ({ mount, page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await mountGuest(mount, page);
    await expect(page.getByTestId(GUEST_TEST_IDS.languageBanner)).toBeVisible();
    await expect(page.getByTestId(GUEST_TEST_IDS.fallbackNote)).toBeVisible();
    await expect(page).toHaveScreenshot('guest-language-banner-390.png');
  });

  test('GuestLanguage screenshot of the sheet', async ({ mount, page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await mountGuest(mount, page);
    await chip(page).click();
    await expect(page.getByTestId(GUEST_TEST_IDS.languageSheet)).toBeVisible();
    await expect(page).toHaveScreenshot('guest-language-sheet-390.png');
  });
});

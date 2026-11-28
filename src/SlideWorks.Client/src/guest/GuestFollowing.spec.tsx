import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { GuestFollowing } from './GuestFollowing';
import { GuestScreens } from './GuestScreens';
import { QA_SLIDE_TEST_IDS as QA } from '../qa/testIds';
import { GUEST_TEST_IDS } from './testIds';

const view = guestView('live');

test('mirrors state.current at the phone width with the waiting card and no sideways scroll', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 740 });
  await mount(<GuestFollowing view={view} state={view.state} status="live" />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);

  await expect(slide).toHaveAttribute('data-slide', '3');
  await expect(slide.getByTestId('slide-3')).toHaveCount(1);
  await expect(page.getByTestId(GUEST_TEST_IDS.progress)).toHaveText('3 / 12');
  await expect(page.getByTestId(GUEST_TEST_IDS.activityWaiting)).toHaveText(
    'Nothing to answer yet',
  );
  const box = await slide.boundingBox();
  expect(box!.width).toBeLessThanOrEqual(360);
  expect(Math.round((box!.width / box!.height) * 100)).toBe(178);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('GuestFollowing_WhenSlideIsChinese_ShouldPutThatScriptsNotoFaceFirst', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const stackOf = async (code: string) => {
    const state = { ...view.state, language: { code, fallbackLines: 0 } };
    await mount(<GuestFollowing view={view} state={state} status="live" />);
    const slide = page.getByTestId(GUEST_TEST_IDS.slide).getByTestId('slide-3');
    return slide.evaluate((el) => ({
      lang: el.closest('[lang]')?.getAttribute('lang'),
      stack: getComputedStyle(el).getPropertyValue('--sw-noto-stack').replaceAll('"', "'"),
    }));
  };
  const hans = await stackOf('zh-Hans');
  expect(hans.lang).toBe('zh-Hans');
  expect(hans.stack).toMatch(/^'Noto Sans Thai', 'Noto Sans SC', 'Noto Sans JP'/);
});

test('GuestFollowing_WhenSlideIsTraditionalChinese_ShouldPutNotoSansTcFirst', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const state = { ...view.state, language: { code: 'zh-Hant', fallbackLines: 0 } };
  await mount(<GuestFollowing view={view} state={state} status="live" />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide).getByTestId('slide-3');
  const stack = await slide.evaluate((el) =>
    getComputedStyle(el).getPropertyValue('--sw-noto-stack').replaceAll('"', "'"),
  );
  expect(stack).toMatch(/^'Noto Sans Thai', 'Noto Sans TC', 'Noto Sans JP'/);
});

test('a black state shows a black screen with no slide in it', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const state = { ...view.state, current: { ...view.state.current!, black: true } };
  await mount(<GuestFollowing view={view} state={state} status="live" />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);

  await expect(slide).toHaveAttribute('data-black', 'true');
  await expect(slide).toHaveCSS('background-color', 'rgb(0, 0, 0)');
  await expect(slide.getByTestId('slide-3')).toHaveCount(0);
});

test('landscape fills the screen with the slide and hides the card', async ({ mount, page }) => {
  await page.setViewportSize({ width: 844, height: 390 });
  await mount(<GuestFollowing view={view} state={view.state} status="live" />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);

  await expect(page.getByTestId(GUEST_TEST_IDS.activity)).toBeHidden();
  const box = await slide.boundingBox();
  expect(Math.round(box!.height)).toBe(390);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('the page follows the presenter: a pushed state moves the slide', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await stubApi(page, { '/api/join/482913': { json: view } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);
  await expect(slide).toHaveAttribute('data-slide', '3');

  hub.pushAudienceEvent(
    audienceState({
      seq: 43,
      current: {
        ...view.state.current!,
        index: 4,
        layout: 'divider',
        slots: { title: 'Next part' },
      } as never,
    }),
  );

  await expect(slide).toHaveAttribute('data-slide', '4');
  await expect(page.getByTestId(GUEST_TEST_IDS.progress)).toHaveText('4 / 12');
});

test('the page shows the ended screen on audienceEnded', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await stubApi(page, { '/api/join/482913': { json: view } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toBeVisible();

  hub.pushAudienceEvent({ type: 'audienceEnded' });

  await expect(page.getByTestId(GUEST_TEST_IDS.ended)).toBeVisible();
});

test('a reconnecting status dims the slide and turns the chip amber', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mount(<GuestFollowing view={view} state={view.state} status="reconnecting" />);
  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toHaveCSS('opacity', '0.6');
  await expect(page.getByTestId(GUEST_TEST_IDS.chip)).toHaveText('Offline');
});

test('a live status shows the green Live chip and a full slide', async ({ mount, page }) => {
  await mount(<GuestFollowing view={view} state={view.state} status="live" />);
  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toHaveCSS('opacity', '1');
  await expect(page.getByTestId(GUEST_TEST_IDS.chip)).toHaveText('Live');
  await expect(page.getByText('Slide 3 of 12')).toHaveCount(1);
});

test("the join slide on a phone shows this page's code and address, live, with no count", async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const current = {
    ...view.state.current!,
    layout: 'join',
    slots: {},
    steps: 0,
    step: 0,
  } as never;
  const state = { ...view.state, current };
  const hub = await stubDeckHub(page, { audience: { state } });
  await stubApi(page, { '/api/join/482913': { json: { ...view, state } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  void hub;
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);
  await expect(slide.getByTestId('join-code')).toHaveText('482 913');
  await expect(slide.getByTestId('join-host')).not.toBeEmpty();
  await expect(slide.getByTestId('join-qr').locator('svg')).toHaveCount(1);
  await expect(slide.getByTestId('join-not-live')).toHaveCount(0);
  await expect(slide.getByTestId('join-count')).toHaveCount(0);
});

test('while following, nothing scrolls at 390x844 or 844x390', async ({ mount, page }) => {
  await page.clock.install();
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  void hub;
  await stubApi(page, { '/api/join/482913': { json: view } });
  await page.setViewportSize({ width: 390, height: 844 });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(GUEST_TEST_IDS.joined)).toBeVisible();
  await page.clock.runFor(2100);
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toBeVisible();
  const scrolls = () =>
    page.evaluate(() => {
      const el = document.documentElement;
      return [el.scrollHeight - el.clientHeight, el.scrollWidth - el.clientWidth];
    });
  expect(await scrolls()).toEqual([0, 0]);
  await expect(page.getByTestId(GUEST_TEST_IDS.privacyOpen)).toBeVisible();

  await page.setViewportSize({ width: 844, height: 390 });
  expect(await scrolls()).toEqual([0, 0]);
  await expect(page.getByTestId(GUEST_TEST_IDS.privacyOpen)).toBeHidden();
  const box = await page.getByTestId(GUEST_TEST_IDS.slide).boundingBox();
  expect(Math.round(box!.height)).toBe(390);
});

test('the following screen shows the night surface with no flat colour of its own', async ({
  mount,
  page,
}) => {
  await mount(<GuestFollowing view={view} state={view.state} status="live" />);
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toHaveCSS(
    'background-color',
    'rgba(0, 0, 0, 0)',
  );
});

// The screens a guest sees, through the shell that paints the night surface, in a live state on the current slide. The
// look has no logo: guest images need a real image behind them, and the baseline must not show a broken one.
async function mountFollowingScreen(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Parameters<Parameters<typeof test>[2]>[0]['page'],
) {
  await page.clock.install();
  const withoutLogo = { ...view, look: { ...view.look, logo: null } };
  await mount(
    <GuestScreens view={withoutLogo} state={withoutLogo.state} status="live" onRetry={() => {}} />,
  );
  await page.clock.runFor(2100);
  await expect(page.getByTestId(GUEST_TEST_IDS.following)).toBeVisible();
  await expect(page.getByTestId(GUEST_TEST_IDS.slide).getByTestId('slide-3')).toHaveCount(1);
}

test('GuestFollowing screenshot at 390 wide', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mountFollowingScreen(mount, page);
  await expect(page).toHaveScreenshot('guest-following-390.png');
});

test('GuestFollowing screenshot in landscape', async ({ mount, page }) => {
  await page.setViewportSize({ width: 844, height: 390 });
  await mountFollowingScreen(mount, page);
  await expect(page).toHaveScreenshot('guest-following-landscape.png');
});

test('live with no current shows the waiting placeholder, then the slide when a state arrives', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const empty = { ...view.state, current: null };
  const hub = await stubDeckHub(page, { audience: { state: empty } });
  await stubApi(page, { '/api/join/482913': { json: { ...view, state: empty } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  const placeholder = page.getByTestId(GUEST_TEST_IDS.waitingForPresenter);
  await expect(placeholder).toHaveText('Waiting for the presenter');
  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toHaveCount(0);
  await expect(page.getByTestId(GUEST_TEST_IDS.progress)).toHaveCount(0);
  await expect(page.getByTestId(GUEST_TEST_IDS.activity)).toBeVisible();
  const box = await placeholder.boundingBox();
  expect(Math.round((box!.width / box!.height) * 100)).toBe(178);

  hub.pushAudienceEvent(audienceState({ seq: 99 }));

  await expect(page.getByTestId(GUEST_TEST_IDS.slide)).toHaveAttribute('data-slide', '3');
  await expect(placeholder).toHaveCount(0);
});

// A guest's phone draws no question list: its Q&A slide is the title over a blank body while the room can ask, and says
// "Questions are off" only when the room has no round.
const qaState = (room: typeof view.state.room) => ({
  ...view.state,
  room,
  current: { ...view.state.current!, layout: 'qa', slots: {}, steps: 0, step: 0 } as never,
});
const openRoom = {
  round: 'r1',
  questions: 'open' as const,
  names: true,
  reactions: true,
  listRevision: 1,
};

test('a live qa slide with questions open does not say "Questions are off" or "Not live"', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mount(<GuestFollowing view={view} state={qaState(openRoom)} status="live" />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);
  await expect(slide.getByTestId(QA.slide)).toHaveCount(1);
  await expect(slide.getByTestId(QA.title)).toHaveText('Questions from the room');
  await expect(slide.getByTestId(QA.off)).toHaveCount(0);
  await expect(slide.getByTestId(QA.notLive)).toHaveCount(0);
  await expect(slide.getByTestId(QA.notLivePill)).toHaveCount(0);
  await expect(page.getByText('Questions are off')).toHaveCount(0);
});

test('a live qa slide whose room has no round says "Questions are off"', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const room = { ...openRoom, round: null, questions: 'off' as const, listRevision: 0 };
  await mount(<GuestFollowing view={view} state={qaState(room)} status="live" />);
  const slide = page.getByTestId(GUEST_TEST_IDS.slide);
  await expect(slide.getByTestId(QA.off)).toContainText('Questions are off');
  await expect(slide.getByTestId(QA.notLive)).toHaveCount(0);
});

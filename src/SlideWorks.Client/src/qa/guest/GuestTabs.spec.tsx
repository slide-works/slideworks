import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PollActivity } from '../../polls/types';
import { AskSheetProbe } from '../../test-support/AskSheetProbe';
import { audienceState } from '../../test-support/fixtures/guestStates';
import { pollActivity } from '../../test-support/fixtures/pollStates';
import { guestQuestion, guestQuestions, qaRoom } from '../../test-support/fixtures/qaStates';
import { stubQuestionsApi } from '../../test-support/stubQuestionsApi';
import { QA_REACTION_ROW_TEST_IDS, QA_TABS_TEST_IDS } from '../testIds';
import type { GuestQuestion, QaRoom } from '../types';
import { GuestQa } from './GuestQa';
import { GuestTabs, type GuestTab } from './GuestTabs';
import { PhoneReactionsFixture } from './PhoneReactionsFixture';
import { QuestionsFixture } from './QuestionsFixture';

// The guest page's tabs (Q8, D1, D7; mockup phone states 1 and 9) on a 360 px phone. The count tests mount GuestTabs over
// the fixtures; the poll-switch tests mount GuestQa, which holds the tab, over the questions stub API, with
// AskSheetProbe as the Slide tab's content to read and drive useAskSheet(). The view sample's open questions are 1, 2
// and 6; 3 is answered and 4 is the guest's own, waiting.
test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

const VIEW = guestQuestions('view');
const OPEN_POLL = pollActivity('open');

// GuestTabs over the fixtures, laid out as GuestFollowing's page lays it out, on the guest page's dark background.
function tabs({
  questions = VIEW.questions,
  tab = 'slide',
  activity = null,
  onTab = () => {},
}: {
  questions?: GuestQuestion[];
  tab?: GuestTab;
  activity?: PollActivity | null;
  onTab?: (tab: GuestTab) => void;
} = {}) {
  return (
    <QuestionsFixture room={VIEW.room} questions={questions} mine={VIEW.mine}>
      <PhoneReactionsFixture>
        <div
          style={{
            height: 740,
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
            padding: '16px 16px 12px',
            background: 'var(--sw-night)',
            color: 'white',
          }}
        >
          <GuestTabs
            tab={tab}
            onTab={onTab}
            activity={activity}
            room={VIEW.room}
            presenter="Sam Rivera"
          >
            <div
              data-testid="slide-content"
              style={{ aspectRatio: '16 / 9', borderRadius: 10, background: '#f7f8fa' }}
            />
            <div
              style={{ flex: 1, borderRadius: 14, border: '1px dashed rgb(255 255 255 / .15)' }}
            />
          </GuestTabs>
        </div>
      </PhoneReactionsFixture>
    </QuestionsFixture>
  );
}

// The real GuestQa, which holds the tab, with the audience state's activity and room.
const guest = (activity: PollActivity | null, room: QaRoom | null = qaRoom('open'), seq = 1) => (
  <GuestQa
    code="482913"
    state={audienceState({ activity, room, seq })}
    status="live"
    presenter="Sam Rivera"
  >
    <AskSheetProbe />
  </GuestQa>
);

const slideTab = (page: Page) => page.getByTestId(QA_TABS_TEST_IDS.slideTab);
const questionsTab = (page: Page) => page.getByTestId(QA_TABS_TEST_IDS.questionsTab);
const dot = (page: Page) => page.getByTestId(QA_TABS_TEST_IDS.slideDot);
const pollJustOpened = (page: Page) => page.getByTestId('probe-poll-just-opened');
// The probe sits in the Slide panel, which is hidden while Questions is showing.
const probe = (page: Page, action: 'show' | 'close' | 'go-to-poll') =>
  page.getByTestId(`probe-sheet-${action}`).dispatchEvent('click');

test('the Questions tab shows and names the count of open questions, and follows it', async ({
  mount,
  page,
}) => {
  const component = await mount(tabs());
  const badge = page.getByTestId(QA_TABS_TEST_IDS.questionsBadge);

  await expect(questionsTab(page)).toHaveAccessibleName('Questions, 3 open');
  await expect(badge).toHaveText('3');

  await component.update(tabs({ questions: [...VIEW.questions, guestQuestion(6, { number: 7 })] }));
  await expect(questionsTab(page)).toHaveAccessibleName('Questions, 4 open');
  await expect(badge).toHaveText('4');

  await component.update(
    tabs({ questions: VIEW.questions.map((q) => ({ ...q, state: 'answered' as const })) }),
  );
  await expect(questionsTab(page)).toHaveAccessibleName('Questions');
  await expect(badge).toHaveCount(0);
});

test('the Slide tab carries a dot named "A poll is open" only while a poll is open and Questions is showing', async ({
  mount,
  page,
}) => {
  const component = await mount(tabs({ tab: 'questions', activity: OPEN_POLL }));
  await expect(dot(page)).toHaveAccessibleName('A poll is open');
  await expect(slideTab(page)).toHaveAccessibleName('Slide A poll is open');

  await component.update(tabs({ tab: 'slide', activity: OPEN_POLL }));
  await expect(dot(page)).toHaveCount(0);

  await component.update(tabs({ tab: 'questions', activity: pollActivity('closed') }));
  await expect(dot(page)).toHaveCount(0);
  await expect(slideTab(page)).toHaveAccessibleName('Slide');
});

test('a poll opening while Questions is showing switches to Slide', async ({ mount, page }) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(pollActivity('draft')));
  await questionsTab(page).click();
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');

  await component.update(guest(OPEN_POLL, qaRoom('open'), 2));

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId(QA_TABS_TEST_IDS.slidePanel)).toBeVisible();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeHidden();
  await expect(dot(page)).toHaveCount(0);
  await expect(pollJustOpened(page)).toHaveText('false');
});

test('a poll opening while the ask sheet is open does not switch, and pollJustOpened turns true until the sheet closes', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(null));
  await questionsTab(page).click();
  await probe(page, 'show');
  await expect(page.getByTestId('probe-sheet-open')).toHaveText('true');
  await expect(pollJustOpened(page)).toHaveText('false');

  await component.update(guest(OPEN_POLL, qaRoom('open'), 2));

  await expect(pollJustOpened(page)).toHaveText('true');
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(dot(page)).toBeVisible();

  await probe(page, 'close');

  await expect(pollJustOpened(page)).toHaveText('false');
  await expect(page.getByTestId('probe-sheet-open')).toHaveText('false');
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
});

test('Go to poll closes the sheet and shows the Slide tab', async ({ mount, page }) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(null));
  await questionsTab(page).click();
  await probe(page, 'show');
  await component.update(guest(OPEN_POLL, qaRoom('open'), 2));
  await expect(pollJustOpened(page)).toHaveText('true');

  await probe(page, 'go-to-poll');

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId('probe-sheet-open')).toHaveText('false');
  await expect(pollJustOpened(page)).toHaveText('false');
});

test('a poll already open when the guest chose Questions does not pull them back', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(OPEN_POLL));
  await questionsTab(page).click();
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(dot(page)).toBeVisible();

  // A later state for the same open poll (a new seq, a moved count) is not a poll opening.
  await component.update(guest({ ...OPEN_POLL }, qaRoom('open', { listRevision: 58 }), 2));

  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeVisible();
});

test('a second poll opening after the first closed switches again', async ({ mount, page }) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(null));
  await questionsTab(page).click();
  await component.update(guest(OPEN_POLL, qaRoom('open'), 2));
  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');

  await component.update(guest(pollActivity('closed'), qaRoom('open'), 3));
  await questionsTab(page).click();
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
  await component.update(guest(OPEN_POLL, qaRoom('open'), 4));

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
});

test('no tab bar and no poll switch when questions are off, and the reaction row still follows reactions', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page, { view: { ...VIEW, room: qaRoom('off') } });
  const component = await mount(guest(null, qaRoom('off')));

  await component.update(guest(OPEN_POLL, qaRoom('off'), 2));

  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toHaveCount(0);
  await expect(dot(page)).toHaveCount(0);
  await expect(page.getByTestId('probe-sheet-open')).toBeVisible();
  await expect(page.getByTestId(QA_REACTION_ROW_TEST_IDS.row)).toBeVisible();
});

test('Slide is selected at first; choosing Questions and then Slide swaps the tabs, the panels and aria-selected', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page, { view: VIEW });
  await mount(guest(null));
  const slidePanel = page.getByTestId(QA_TABS_TEST_IDS.slidePanel);
  const questionsPanel = page.getByTestId(QA_TABS_TEST_IDS.questionsPanel);
  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'false');
  await expect(slidePanel).toBeVisible();
  await expect(questionsPanel).toBeHidden();

  await questionsTab(page).click();
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'false');
  await expect(questionsPanel).toBeVisible();
  await expect(slidePanel).toBeHidden();

  await slideTab(page).click();
  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'false');
  await expect(slidePanel).toBeVisible();
  await expect(questionsPanel).toBeHidden();
});

test('the arrow keys, Home and End move between the tabs and select them; only the selected tab is a tab stop', async ({
  mount,
  page,
}) => {
  await mount(guest(null));
  await expect(slideTab(page)).toHaveAttribute('tabindex', '0');
  await expect(questionsTab(page)).toHaveAttribute('tabindex', '-1');

  await slideTab(page).focus();
  await page.keyboard.press('ArrowRight');
  await expect(questionsTab(page)).toBeFocused();
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(questionsTab(page)).toHaveAttribute('tabindex', '0');
  await expect(slideTab(page)).toHaveAttribute('tabindex', '-1');

  await page.keyboard.press('ArrowLeft');
  await expect(slideTab(page)).toBeFocused();
  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');

  await page.keyboard.press('End');
  await expect(questionsTab(page)).toBeFocused();
  await page.keyboard.press('Home');
  await expect(slideTab(page)).toBeFocused();
  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
});

test('both tabs are at least 44 px tall', async ({ mount, page }) => {
  await mount(tabs());
  for (const tab of [slideTab(page), questionsTab(page)])
    expect((await tab.boundingBox())!.height).toBeGreaterThanOrEqual(44);
});

test('the count badge is hidden from assistive tech, so the name holds the count once', async ({
  mount,
  page,
}) => {
  await mount(tabs());
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsBadge)).toHaveAttribute(
    'aria-hidden',
    'true',
  );
});

test('a different round replacing an open poll while Questions is showing switches to Slide (D2)', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(OPEN_POLL));
  await questionsTab(page).click();
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'true');

  await component.update(
    guest(
      { ...OPEN_POLL, round: `${OPEN_POLL.round ?? 0}-next`, number: OPEN_POLL.number + 1 },
      qaRoom('open'),
      2,
    ),
  );

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(questionsTab(page)).toHaveAttribute('aria-selected', 'false');
});

test('a round change alone, with the same poll number, also switches', async ({ mount, page }) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(OPEN_POLL));
  await questionsTab(page).click();

  await component.update(
    guest({ ...OPEN_POLL, round: `${OPEN_POLL.round ?? 0}-next` }, qaRoom('open'), 2),
  );

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
});

test('no tab bar and no switch when the room is null', async ({ mount, page }) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(null, null));
  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toHaveCount(0);
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toHaveCount(0);

  await component.update(guest(OPEN_POLL, null, 2));

  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toHaveCount(0);
  await expect(page.getByTestId('probe-sheet-open')).toBeVisible();
});

test('the switch is announced in a status region and focus inside Questions moves to the Slide tab', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(null));
  await questionsTab(page).click();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeVisible();
  // The ask bar's button, or whatever comes first, is in the panel.
  await page
    .getByTestId(QA_TABS_TEST_IDS.questionsPanel)
    .locator('button:not([disabled]), input, textarea, [tabindex="0"]')
    .first()
    .focus();
  expect(await page.evaluate(() => !!document.activeElement?.closest('#qa-panel-questions'))).toBe(
    true,
  );
  const status = page.getByRole('status').filter({ hasText: /poll/ });
  await expect(status).toHaveCount(0);

  await component.update(guest(OPEN_POLL, qaRoom('open'), 2));

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(slideTab(page)).toBeFocused();
  await expect(status).toHaveText('A poll just opened. Showing the Slide tab.');
});

test('the switch leaves focus alone when it was not inside Questions', async ({ mount, page }) => {
  await stubQuestionsApi(page, { view: VIEW });
  const component = await mount(guest(null));
  await questionsTab(page).click();
  await expect(questionsTab(page)).toBeFocused();

  await component.update(guest(OPEN_POLL, qaRoom('open'), 2));

  await expect(slideTab(page)).toHaveAttribute('aria-selected', 'true');
  await expect(slideTab(page)).not.toBeFocused();
});

test('in landscape with Questions chosen, the slide still shows and the Questions panel hides', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 844, height: 390 });
  await mount(tabs({ tab: 'questions' }));

  await expect(page.getByTestId('slide-content')).toBeVisible();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.slidePanel)).toHaveCSS('display', 'contents');
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeHidden();
});

test('in landscape the reaction row hides with the tab bar', async ({ mount, page }) => {
  await page.setViewportSize({ width: 844, height: 390 });
  await mount(tabs());

  await expect(page.getByTestId('slide-content')).toBeVisible();
  await expect(page.getByTestId(QA_REACTION_ROW_TEST_IDS.row)).toBeHidden();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toBeHidden();
});

test('reduced motion turns the tab transition off', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  const component = await mount(tabs());
  await expect(slideTab(page)).not.toHaveCSS('transition-duration', '0s');
  await component.unmount();

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mount(tabs());
  await expect(slideTab(page)).toHaveCSS('transition-duration', '0s');
  await expect(questionsTab(page)).toHaveCSS('transition-duration', '0s');
});

test('nothing scrolls sideways at 360 px', async ({ mount, page }) => {
  await mount(tabs({ tab: 'questions', activity: OPEN_POLL }));
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('screenshot the Slide tab with reactions and the open count', async ({ mount, page }) => {
  await mount(tabs());
  await expect(page).toHaveScreenshot('qa-tabs-slide.png');
});

test('screenshot Questions with a poll open', async ({ mount, page }) => {
  await mount(tabs({ tab: 'questions', activity: OPEN_POLL }));
  await expect(page).toHaveScreenshot('qa-tabs-poll-dot.png');
});

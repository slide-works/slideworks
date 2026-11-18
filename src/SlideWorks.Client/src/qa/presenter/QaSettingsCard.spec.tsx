import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { guestView } from '../../test-support/fixtures/guestStates';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { deckPage, me } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubQaControlApi, type QaControlApiCall } from '../../test-support/stubQaControlApi';
import { QA_SETTINGS_CARD_TEST_IDS as IDS } from '../testIds';
import type { QaControlView, QaSettings } from '../types';
import { QaControlProbe } from '../../test-support/QaControlProbe';
import { QaControlFixture } from './QaControlFixture';
import { QaControlLive } from './QaControlLive';
import { QaSettingsCard } from './QaSettingsCard';

// The deck page's card: all five settings and the selected slide's switch, live or not; each change sends just its
// field; a failed change shows a message and goes back; a person who can't present gets no card. The live sample:
// questions open, review on, names on, reactions on, showOn anySlide; slide 3 has questions off, every other slide both on.

const DECK = 'deck-sw331';

const withSettings = (settings: Partial<QaSettings>, overrides: Partial<QaControlView> = {}) => {
  const view = qaControlView('live', overrides);
  return { ...view, settings: { ...view.settings, ...settings } };
};

async function stubs(page: Page, deckId: string, control: QaControlView, routes = {}) {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  return stubQaControlApi(page, deckId, { control, routes });
}

const card = (slide: number | null) => (
  <QaControlLive deckId={DECK} slide={null}>
    <QaSettingsCard deckId={DECK} slide={slide} />
  </QaControlLive>
);

const writes = (calls: QaControlApiCall[]) => calls.filter((c) => c.call !== 'read');

test('QaSettingsCard_WhenRead_ShouldShowAllFiveSettings', async ({ mount, page }) => {
  await stubs(page, DECK, withSettings({ questions: 'closed', names: false, showOn: 'qaSlide' }));
  await mount(card(5));
  await expect(page.getByTestId(IDS.title)).toHaveText('Q&A and reactions');
  await expect(page.getByTestId(IDS.questionsClosed)).toBeChecked();
  await expect(page.getByTestId(IDS.questionsOpen)).not.toBeChecked();
  await expect(page.getByTestId(IDS.review)).toBeChecked();
  await expect(page.getByTestId(IDS.names)).not.toBeChecked();
  await expect(page.getByTestId(IDS.reactions)).toBeChecked();
  await expect(page.getByTestId(IDS.showOnQaSlide)).toBeChecked();
  await expect(page.getByTestId(IDS.showOnAnySlide)).not.toBeChecked();
});

test('QaSettingsCard_WhenEachSettingIsChanged_ShouldSendJustThatField', async ({ mount, page }) => {
  const api = await stubs(page, DECK, qaControlView('live'));
  await mount(card(5));
  await page.getByTestId(IDS.questionsClosed).click();
  await expect(page.getByTestId(IDS.questionsClosed)).toBeChecked();
  await page.getByTestId(IDS.questionsOff).click();
  await expect(page.getByTestId(IDS.questionsOff)).toBeChecked();
  await page.getByTestId(IDS.review).click();
  await expect(page.getByTestId(IDS.review)).not.toBeChecked();
  await page.getByTestId(IDS.names).click();
  await expect(page.getByTestId(IDS.names)).not.toBeChecked();
  await page.getByTestId(IDS.reactions).click();
  await expect(page.getByTestId(IDS.reactions)).not.toBeChecked();
  await page.getByTestId(IDS.showOnQaSlide).click();
  await expect(page.getByTestId(IDS.showOnQaSlide)).toBeChecked();

  expect(writes(api.calls)).toEqual([
    { call: 'settings', body: { questions: 'closed' } },
    { call: 'settings', body: { questions: 'off' } },
    { call: 'settings', body: { review: false } },
    { call: 'settings', body: { names: false } },
    { call: 'settings', body: { reactions: false } },
    { call: 'settings', body: { showOn: 'qaSlide' } },
  ]);
});

test('QaSettingsCard_WhenASlideIsSelected_ShouldShowItsSwitchFromTheViewsSlides', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, DECK, qaControlView('live'));
  const shown = await mount(card(3));
  await expect(page.getByTestId(IDS.slideQuestions)).toHaveAccessibleName('Slide 3: questions');
  await expect(page.getByTestId(IDS.slideQuestions)).not.toBeChecked();
  await expect(page.getByTestId(IDS.slideReactions)).toHaveAccessibleName('Slide 3: reactions');
  await expect(page.getByTestId(IDS.slideReactions)).toBeChecked();

  // A slide the view doesn't list has both on.
  await shown.update(card(2));
  await expect(page.getByTestId(IDS.slideQuestions)).toHaveAccessibleName('Slide 2: questions');
  await expect(page.getByTestId(IDS.slideQuestions)).toBeChecked();
  await expect(page.getByTestId(IDS.slideReactions)).toBeChecked();

  await page.getByTestId(IDS.slideReactions).click();
  await expect(page.getByTestId(IDS.slideReactions)).not.toBeChecked();
  expect(writes(api.calls)).toEqual([{ call: 'slide', slide: 2, body: { reactions: false } }]);
});

test('QaSettingsCard_WhenTheDeckHasNoSlides_ShouldShowNoSlideSwitch', async ({ mount, page }) => {
  await stubs(page, DECK, qaControlView('live'));
  await mount(card(null));
  await expect(page.getByTestId(IDS.review)).toBeVisible();
  await expect(page.getByTestId(IDS.slideQuestions)).toHaveCount(0);
  await expect(page.getByTestId(IDS.slideReactions)).toHaveCount(0);
});

test('QaSettingsCard_WhenTheDeckIsNotLive_ShouldStillChangeSettingsAndTheSlideSwitch', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, DECK, qaControlView('notLive'));
  await mount(card(4));
  await page.getByTestId(IDS.review).click();
  await expect(page.getByTestId(IDS.review)).toBeChecked();
  await page.getByTestId(IDS.slideQuestions).click();
  await expect(page.getByTestId(IDS.slideQuestions)).not.toBeChecked();
  expect(writes(api.calls)).toEqual([
    { call: 'settings', body: { review: true } },
    { call: 'slide', slide: 4, body: { questions: false } },
  ]);
});

test('QaSettingsCard_WhenTheDeckHasAFeatureOff_ShouldDisableTheSlideSwitchWithItsReason', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={withSettings({ questions: 'off', reactions: false })}>
      <QaSettingsCard deckId={DECK} slide={5} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.slideQuestions)).toBeDisabled();
  await expect(page.getByTestId(IDS.slideQuestionsReason)).toHaveText(
    'Questions are off for the deck',
  );
  await expect(page.getByTestId(IDS.slideReactions)).toBeDisabled();
  await expect(page.getByTestId(IDS.slideReactionsReason)).toHaveText(
    'Reactions are off for the deck',
  );
});

test('QaSettingsCard_WhenTheDeckHasBothFeaturesOn_ShouldLeaveTheSlideSwitchesEnabledWithNoReason', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={qaControlView('live')}>
      <QaSettingsCard deckId={DECK} slide={5} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.slideQuestions)).toBeEnabled();
  await expect(page.getByTestId(IDS.slideReactions)).toBeEnabled();
  await expect(page.getByTestId(IDS.slideQuestionsReason)).toHaveCount(0);
  await expect(page.getByTestId(IDS.slideReactionsReason)).toHaveCount(0);
});

test('QaSettingsCard_WhenAChangeFails_ShouldShowTheMessageAndPutTheControlBack', async ({
  mount,
  page,
}) => {
  await stubs(page, DECK, qaControlView('live'), {
    settings: { status: 500 },
    slide: { status: 500 },
  });
  await mount(card(5));
  await page.getByTestId(IDS.names).click();
  await expect(page.getByRole('alert')).toContainText("Couldn't change the question settings.");
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(IDS.names)).toBeChecked();

  await page.getByTestId(IDS.questionsClosed).click();
  await expect(page.getByTestId(IDS.questionsOpen)).toBeChecked();
  await expect(page.getByTestId(IDS.questionsClosed)).not.toBeChecked();

  await page.getByTestId(IDS.slideReactions).click();
  await expect(page.getByRole('alert')).toContainText("Couldn't change this slide's switch.");
  await expect(page.getByTestId(IDS.slideReactions)).toBeChecked();
});

test('QaSettingsCard_WhenTheFirstReadFails_ShouldShowAMessageNotLoadingForever', async ({
  mount,
  page,
}) => {
  await stubs(page, DECK, qaControlView('live'), { read: { status: 500 } });
  await mount(card(5));
  await expect(page.getByRole('alert')).toContainText("Couldn't load the questions.");
  await expect(page.getByTestId(IDS.card)).not.toContainText('Loading the settings.');
  await expect(page.getByTestId(IDS.questions)).toHaveCount(0);
});

test('QaSettingsCard_WhenTheSlideSwitchIsDisabled_ShouldDescribeItByItsReason', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={withSettings({ questions: 'off', reactions: false })}>
      <QaSettingsCard deckId={DECK} slide={5} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.slideQuestions)).toHaveAccessibleDescription(
    'Questions are off for the deck',
  );
  await expect(page.getByTestId(IDS.slideReactions)).toHaveAccessibleDescription(
    'Reactions are off for the deck',
  );
});

test('QaSettingsCard_WhenTheDeckHasQuestionsClosed_ShouldKeepTheSlidesQuestionSwitchEnabled', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={withSettings({ questions: 'closed' })}>
      <QaSettingsCard deckId={DECK} slide={5} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.slideQuestions)).toBeEnabled();
  await expect(page.getByTestId(IDS.slideQuestionsReason)).toHaveCount(0);
});

const withProbe = (number: number) => (
  <QaControlLive deckId={DECK} slide={null}>
    <QaSettingsCard deckId={DECK} slide={5} />
    <QaControlProbe deckId={DECK} number={number} />
  </QaControlLive>
);

test('QaSettingsCard_WhenAnotherCallStartsBeforeAFailedChangesFollowUpReadEnds_ShouldStillShowTheFailure', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, DECK, qaControlView('live'), { settings: { status: 500 } });
  await mount(withProbe(1));
  await expect(page.getByTestId(IDS.card)).toBeVisible();
  await expect(page.getByTestId(IDS.review)).toBeVisible();
  const held = api.hold('read');
  await page.getByTestId(IDS.review).click();
  await expect.poll(() => api.calls.filter((c) => c.call === 'read').length).toBe(2);
  await page.getByTestId('probe-qa-show').click();
  await expect.poll(() => api.calls.some((c) => c.call === 'show')).toBe(true);
  held.release();
  await expect(page.getByTestId(IDS.review)).toBeChecked();
  await expect(page.getByRole('alert')).toContainText("Couldn't change the question settings.");
});

test('QaSettingsCard_WhenAModerationActionIsRefusedWhileAChangeIsInFlight_ShouldNotShowTheRefusalInTheCard', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, DECK, qaControlView('live'));
  await mount(withProbe(5));
  await expect(page.getByTestId(IDS.review)).toBeVisible();
  const held = api.hold('settings');
  await page.getByTestId(IDS.review).click();
  await expect.poll(() => api.calls.some((c) => c.call === 'settings')).toBe(true);
  await page.getByTestId('probe-qa-hide').click();
  await expect(page.getByTestId('probe-qa-error')).toContainText(
    'That question had already changed',
  );
  held.release();
  await expect(page.getByTestId(IDS.review)).not.toBeChecked();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaSettingsCard_WhenTheLiveWatchFailedAndAChangeWorks_ShouldNotShowTheWatchMessage', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  hub.fail('WatchAudience', 'Server timeout.');
  await stubQaControlApi(page, DECK, { control: qaControlView('live') });
  await mount(withProbe(1));
  await expect(page.getByTestId('probe-qa-error')).toContainText("aren't reaching this window");
  await page.getByTestId(IDS.names).click();
  await expect(page.getByTestId(IDS.names)).not.toBeChecked();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaSettingsCard_WhenAnotherPartsCallFailed_ShouldNotShowItsMessage', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={qaControlView('live')} error="Couldn't hide it.">
      <QaSettingsCard deckId={DECK} slide={5} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.card)).toBeVisible();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

const deckPath = `/api/decks/${deckPage.id}`;
const route = { hooksConfig: { route: `/decks/${deckPage.id}` } };

test('QaSettingsCard_WhenThePersonOwnsTheDeck_ShouldBeOnTheDeckPageForTheSelectedSlide', async ({
  mount,
  page,
}) => {
  await page.route('**/api/looks', (r) => r.fulfill({ json: { templates: [], palettes: [] } }));
  await stubs(page, deckPage.id, qaControlView('live'));
  await stubApi(page, {
    '/api/me': { json: me },
    [deckPath]: { json: { ...deckPage, isOwner: true } },
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId(IDS.card)).toBeVisible();
  await expect(page.getByTestId(IDS.slideQuestions)).toHaveAccessibleName('Slide 1: questions');
});

test('QaSettingsCard_WhenThePersonCannotPresent_ShouldNotBeRendered', async ({ mount, page }) => {
  const api = await stubs(page, deckPage.id, qaControlView('live'));
  await stubApi(page, {
    '/api/me': { json: me },
    [deckPath]: { json: { ...deckPage, isOwner: false } },
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('deck-page')).toBeVisible();
  await expect(page.getByTestId(IDS.card)).toHaveCount(0);
  expect(api.calls).toEqual([]);
});

const lightCard = (control: QaControlView, slide: number | null) => (
  <div style={{ width: 288 }}>
    <QaControlFixture deckId={DECK} control={control}>
      <QaSettingsCard deckId={DECK} slide={slide} />
    </QaControlFixture>
  </div>
);

test('screenshot settings card live', async ({ mount, page }) => {
  await mount(lightCard(qaControlView('live'), 3));
  await expect(page.getByTestId(IDS.card)).toHaveScreenshot('card-live.png');
});

test('screenshot settings card with the deck features off', async ({ mount, page }) => {
  await mount(lightCard(withSettings({ questions: 'off', reactions: false }), 5));
  await expect(page.getByTestId(IDS.card)).toHaveScreenshot('card-features-off.png');
});

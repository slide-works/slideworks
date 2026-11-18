import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestView } from '../../test-support/fixtures/guestStates';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubQaControlApi, type QaControlApiCall } from '../../test-support/stubQaControlApi';
import { QA_SETTINGS_STRIP_TEST_IDS as IDS } from '../testIds';
import type { QaControlView, QaSettings } from '../types';
import { QaControlFixture } from './QaControlFixture';
import { QaControlLive } from './QaControlLive';
import { QaSettingsStrip } from './QaSettingsStrip';

// The settings strip: each toggle sends one change with just its field, the slide's two switches follow the view's
// `here`, a switch is disabled with its reason when the deck has that feature off, and a failed change shows a message
// and goes back. The live sample: review on, names on, reactions on, questions open, the presenter on slide 5 (both
// switches on); slide 3 has questions off.

const DECK = 'deck-sw331';

const withSettings = (settings: Partial<QaSettings>, overrides: Partial<QaControlView> = {}) => {
  const view = qaControlView('live', overrides);
  return { ...view, settings: { ...view.settings, ...settings } };
};

async function stubs(page: Page, control: QaControlView, routes = {}) {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  return stubQaControlApi(page, DECK, { control, routes });
}

const strip = (
  <QaControlLive deckId={DECK} slide={5}>
    <QaSettingsStrip deckId={DECK} />
  </QaControlLive>
);

const writes = (calls: QaControlApiCall[]) => calls.filter((c) => c.call !== 'read');

test('QaSettingsStrip_WhenEachDeckToggleIsPressed_ShouldSendJustThatField', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, qaControlView('live'));
  await mount(strip);
  await expect(page.getByTestId(IDS.questions)).toHaveText('Questions open');
  await expect(page.getByTestId(IDS.review)).toHaveText('Review first');
  await expect(page.getByTestId(IDS.names)).toHaveText('Names allowed');
  await expect(page.getByTestId(IDS.reactions)).toHaveText('Reactions on');

  await page.getByTestId(IDS.questions).click();
  await expect(page.getByTestId(IDS.questions)).toHaveText('Questions closed');
  await page.getByTestId(IDS.review).click();
  await expect(page.getByTestId(IDS.review)).toHaveText('Review off');
  await page.getByTestId(IDS.names).click();
  await expect(page.getByTestId(IDS.names)).toHaveText('Names off');
  await page.getByTestId(IDS.reactions).click();
  await expect(page.getByTestId(IDS.reactions)).toHaveText('Reactions off');

  expect(writes(api.calls)).toEqual([
    { call: 'settings', body: { questions: 'closed' } },
    { call: 'settings', body: { review: false } },
    { call: 'settings', body: { names: false } },
    { call: 'settings', body: { reactions: false } },
  ]);
});

test('QaSettingsStrip_WhenQuestionsAreClosedOrOff_ShouldOpenThemWithOnePress', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, withSettings({ questions: 'closed' }));
  await mount(strip);
  await page.getByTestId(IDS.questions).click();
  await expect(page.getByTestId(IDS.questions)).toHaveText('Questions open');
  expect(writes(api.calls)).toEqual([{ call: 'settings', body: { questions: 'open' } }]);
});

test('QaSettingsStrip_WhenTheSlideSwitchesArePressed_ShouldChangeOnlyThatSwitchOnThePresentersSlide', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, qaControlView('live'));
  await mount(strip);
  await expect(page.getByTestId(IDS.slideQuestions)).toBeChecked();
  await expect(page.getByTestId(IDS.slideReactions)).toBeChecked();

  await page.getByTestId(IDS.slideQuestions).click();
  await expect(page.getByTestId(IDS.slideQuestions)).not.toBeChecked();
  await page.getByTestId(IDS.slideReactions).click();
  await expect(page.getByTestId(IDS.slideReactions)).not.toBeChecked();
  await page.getByTestId(IDS.slideQuestions).click();
  await expect(page.getByTestId(IDS.slideQuestions)).toBeChecked();

  expect(writes(api.calls)).toEqual([
    { call: 'slide', slide: 5, body: { questions: false } },
    { call: 'slide', slide: 5, body: { reactions: false } },
    { call: 'slide', slide: 5, body: { questions: true } },
  ]);
});

test('QaSettingsStrip_WhenTheDeckIsNotLive_ShouldShowTheTogglesAndNoSlideSwitches', async ({
  mount,
  page,
}) => {
  const api = await stubs(page, qaControlView('notLive'));
  await mount(strip);
  await expect(page.getByTestId(IDS.review)).toBeVisible();
  await expect(page.getByTestId(IDS.slideQuestions)).toHaveCount(0);
  await expect(page.getByTestId(IDS.slideReactions)).toHaveCount(0);
  await page.getByTestId(IDS.names).click();
  await expect(page.getByTestId(IDS.names)).toHaveText('Names off');
  expect(writes(api.calls)).toEqual([{ call: 'settings', body: { names: false } }]);
});

test('QaSettingsStrip_WhenTheDeckHasQuestionsOff_ShouldDisableTheSlidesQuestionSwitchWithItsReason', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={withSettings({ questions: 'off' })}>
      <QaSettingsStrip deckId={DECK} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.questions)).toHaveText('Questions off');
  await expect(page.getByTestId(IDS.slideQuestions)).toBeDisabled();
  await expect(page.getByTestId(IDS.slideQuestionsReason)).toHaveText(
    'Questions are off for the deck',
  );
  await expect(page.getByTestId(IDS.slideReactions)).toBeEnabled();
  await expect(page.getByTestId(IDS.slideReactionsReason)).toHaveCount(0);
});

test('QaSettingsStrip_WhenTheDeckHasReactionsOff_ShouldDisableTheSlidesReactionSwitchWithItsReason', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={withSettings({ reactions: false })}>
      <QaSettingsStrip deckId={DECK} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.slideReactions)).toBeDisabled();
  await expect(page.getByTestId(IDS.slideReactionsReason)).toHaveText(
    'Reactions are off for the deck',
  );
  await expect(page.getByTestId(IDS.slideQuestions)).toBeEnabled();
  await expect(page.getByTestId(IDS.slideQuestionsReason)).toHaveCount(0);
});

test('QaSettingsStrip_WhenAChangeFails_ShouldShowTheMessageAndPutTheControlBack', async ({
  mount,
  page,
}) => {
  await stubs(page, qaControlView('live'), { settings: { status: 500 }, slide: { status: 500 } });
  await mount(strip);
  await expect(page.getByTestId(IDS.review)).toHaveAttribute('aria-pressed', 'true');
  await page.getByTestId(IDS.review).click();
  await expect(page.getByTestId(IDS.error)).toContainText("Couldn't change the question settings.");
  await expect(page.getByTestId(IDS.review)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(IDS.review)).toHaveText('Review first');

  await page.getByTestId(IDS.slideQuestions).click();
  await expect(page.getByTestId(IDS.error)).toContainText("Couldn't change this slide's switch.");
  await expect(page.getByTestId(IDS.slideQuestions)).toBeChecked();
});

test('QaSettingsStrip_WhenAnotherPartsCallFailed_ShouldNotShowItsMessage', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId={DECK} control={qaControlView('live')} error="Couldn't hide it.">
      <QaSettingsStrip deckId={DECK} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.strip)).toBeVisible();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaSettingsStrip_WhenTheViewIsNotRead_ShouldDrawNothing', async ({ mount, page }) => {
  await mount(
    <QaControlFixture deckId={DECK} control={null}>
      <QaSettingsStrip deckId={DECK} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(IDS.strip)).toHaveCount(0);
});

const darkPanel = (control: QaControlView, error: string | null = null) => (
  <div style={{ background: '#111827', padding: 12, width: 640 }}>
    <QaControlFixture deckId={DECK} control={control} error={error}>
      <QaSettingsStrip deckId={DECK} />
    </QaControlFixture>
  </div>
);

test('screenshot settings strip live', async ({ mount, page }) => {
  await mount(darkPanel(withSettings({ review: false })));
  await expect(page.getByTestId(IDS.strip)).toHaveScreenshot('strip-live.png');
});

test('screenshot settings strip with the deck features off', async ({ mount, page }) => {
  await mount(darkPanel(withSettings({ questions: 'off', reactions: false })));
  await expect(page.getByTestId(IDS.strip)).toHaveScreenshot('strip-features-off.png');
});

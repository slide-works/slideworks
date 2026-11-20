import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import type { AudienceCurrent } from '../../guest/audienceEvents';
import { guestView } from '../../test-support/fixtures/guestStates';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { OPEN_RESPONSE_ERRORS } from '../errors';
import { stubOpenResponses } from '../stubOpenResponses';
import { OPEN_RESPONSE_TEST_IDS as IDS } from '../testIds';
import type { GuestResponseView, WordCloudEvent } from '../types';
import { OpenAnswerCard } from './OpenAnswerCard';
import { WordCard } from './WordCard';

const QUESTION = 'One word for today?';
const ROUND = { phase: 'open', round: 'round_1', number: 1, revision: 4, revealed: true } as const;

const wordsView = (mineWords: string[]): GuestResponseView => ({
  ...ROUND,
  kind: 'word-cloud',
  question: QUESTION,
  guests: 2,
  mineWords,
});

const textView = (mineText: string): GuestResponseView => ({
  ...ROUND,
  kind: 'open-text',
  question: QUESTION,
  guests: 2,
  mineText,
});

const PROFANE = { status: 409, json: { error: 'profane', message: 'That word is not allowed.' } };
const STALE = { status: 409, json: { error: 'stale', message: 'Stale.' } };

const DRAFT: GuestResponseView = {
  phase: 'draft',
  number: 1,
  revision: 0,
  revealed: true,
  kind: 'word-cloud',
  question: QUESTION,
  guests: 0,
  mineWords: [],
};

type Reply = { status: number; json: unknown };

// Holds the nth GET of a slide's response until release, then answers it with reply or lets it through to the stub.
async function holdRead(page: Parameters<typeof stubOpenResponses>[0], slide: number, nth: number) {
  let reach = () => {};
  const reached = new Promise<void>((resolve) => (reach = resolve));
  let release: (reply?: Reply) => void = () => {};
  const released = new Promise<Reply | undefined>((resolve) => (release = resolve));
  let reads = 0;
  await page.route(`**/api/join/482913/slides/${slide}/response`, async (route) => {
    reads += 1;
    if (reads !== nth) return route.fallback();
    reach();
    const reply = await released;
    return reply ? route.fulfill(reply) : route.fallback();
  });
  return { reached, release: (reply?: Reply) => release(reply) };
}

test('WordCard_WhenTheGetReturnsWords_ShouldShowThoseWords', async ({ mount, page }) => {
  await stubOpenResponses(page, { slide: 3, view: wordsView(['Calm', 'Ready']) });
  await mount(<WordCard code="482913" slide={3} status="live" />);

  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);
  await expect(page.getByTestId(IDS.mineEntry)).toHaveText(['Calm', 'Ready']);
  await expect(page.getByRole('textbox', { name: `${QUESTION} (word 1 of 3)` })).toBeVisible();
});

test('WordCard_WhenTheSubmitIsProfane_ShouldShowTheSentence', async ({ mount, page }) => {
  const stub = await stubOpenResponses(page, { slide: 3, view: wordsView([]) });
  stub.reply(PROFANE);
  await mount(<WordCard code="482913" slide={3} status="live" />);
  const first = page.getByTestId(IDS.input).first();

  await first.fill('rude');
  await page.getByTestId(IDS.submit).click();

  await expect(page.getByTestId(IDS.error)).toHaveText(OPEN_RESPONSE_ERRORS.profane);
  await expect(first).toBeFocused();
  expect(stub.sends).toEqual([{ round: 'round_1', revision: 4, words: ['rude'] }]);
  await expect(page.getByTestId(IDS.mine)).toHaveCount(0);
});

test('WordCard_WhenTheSubmitIsStale_ShouldReadTheViewAgain', async ({ mount, page }) => {
  const stub = await stubOpenResponses(page, { slide: 3, view: wordsView([]) });
  stub.reply({ status: 409, json: { error: 'stale', message: 'Stale.' } });
  await mount(<WordCard code="482913" slide={3} status="live" />);
  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);

  await page.getByTestId(IDS.input).first().fill('Calm');
  await page.getByTestId(IDS.submit).click();

  await expect(page.getByTestId(IDS.error)).toHaveText(OPEN_RESPONSE_ERRORS.stale);
  await expect.poll(() => stub.gets).toBe(2);
});

test('OpenAnswerCard_WhenTheGetReturnsText_ShouldShowThatText', async ({ mount, page }) => {
  await stubOpenResponses(page, { slide: 3, view: textView('More demos please') });
  await mount(<OpenAnswerCard code="482913" slide={3} status="live" />);

  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);
  await expect(page.getByTestId(IDS.mine)).toHaveText('More demos please');
  await expect(page.getByRole('textbox', { name: `${QUESTION} (your answer)` })).toBeVisible();
});

test('OpenAnswerCard_WhenTheSubmitIsProfane_ShouldShowTheSentence', async ({ mount, page }) => {
  const stub = await stubOpenResponses(page, { slide: 3, view: textView('') });
  stub.reply(PROFANE);
  await mount(<OpenAnswerCard code="482913" slide={3} status="live" />);
  const box = page.getByTestId(IDS.input);

  await box.fill('rude answer');
  await page.getByTestId(IDS.submit).click();

  await expect(page.getByTestId(IDS.error)).toHaveText(OPEN_RESPONSE_ERRORS.profane);
  await expect(box).toBeFocused();
  expect(stub.sends).toEqual([{ round: 'round_1', revision: 4, text: 'rude answer' }]);
  await expect(page.getByTestId(IDS.mine)).toHaveCount(0);
});

test('OpenAnswerCard_WhenTheSubmitIsStored_ShouldShowTheAnswer', async ({ mount, page }) => {
  await stubOpenResponses(page, { slide: 3, view: textView('') });
  await mount(<OpenAnswerCard code="482913" slide={3} status="live" />);
  const box = page.getByTestId(IDS.input);

  await box.fill('Ship it');
  await page.getByTestId(IDS.submit).click();

  await expect(page.getByTestId(IDS.mine)).toHaveText('Ship it');
  await expect(box).toHaveValue('');
  await expect(box).toBeFocused();
});

const view = guestView('live');

const on = (layout: 'word-cloud' | 'open-text'): AudienceCurrent =>
  ({ ...view.state.current!, layout, slots: { question: QUESTION } }) as AudienceCurrent;

const liveOn = (layout: 'word-cloud' | 'open-text') => ({
  ...view,
  state: { ...view.state, current: on(layout) },
});

test('Following_WhenTheLayoutIsWordCloud_ShouldMountTheCard', async ({ mount, page }) => {
  const guest = liveOn('word-cloud');
  const hub = await stubDeckHub(page, { audience: { state: guest.state } });
  await stubApi(page, { '/api/join/482913': { json: guest } });
  await stubOpenResponses(page, { slide: 3, view: wordsView(['Calm']) });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(IDS.wordCard)).toBeVisible();
  await expect(page.getByTestId(IDS.mineEntry)).toHaveText(['Calm']);

  hub.pushWordCloud({
    ...ROUND,
    type: 'wordCloud',
    slide: 3,
    guests: 2,
    words: [
      { text: 'Calm', count: 2, hidden: false },
      { text: 'Tired', count: 1, hidden: false },
    ],
  });
  await expect(page.getByTestId(IDS.roomEntry)).toHaveText(['Calm', 'Tired']);
});

test('Following_WhenTheLayoutIsOpenText_ShouldMountTheCard', async ({ mount, page }) => {
  const guest = liveOn('open-text');
  await stubDeckHub(page, { audience: { state: guest.state } });
  await stubApi(page, { '/api/join/482913': { json: guest } });
  await stubOpenResponses(page, { slide: 3, view: textView('Ship it') });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });

  await expect(page.getByTestId(IDS.answerCard)).toBeVisible();
  await expect(page.getByTestId(IDS.mine)).toHaveText('Ship it');
});

test('WordCard_WhenTheHubOpensADraftRound_ShouldSendForThatRoundAndCloseWithIt', async ({
  mount,
  page,
}) => {
  const guest = liveOn('word-cloud');
  const hub = await stubDeckHub(page, { audience: { state: guest.state } });
  await stubApi(page, { '/api/join/482913': { json: guest } });
  const stub = await stubOpenResponses(page, { slide: 3, view: DRAFT });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);
  await expect(page.getByTestId(IDS.submit)).toBeDisabled();

  const open: WordCloudEvent = { ...ROUND, type: 'wordCloud', slide: 3, guests: 0, words: [] };
  hub.pushWordCloud({ ...open, revision: 1 });
  await expect(page.getByTestId(IDS.submit)).toBeEnabled();
  await page.getByTestId(IDS.input).first().fill('Calm');
  await page.getByTestId(IDS.submit).click();
  await expect(page.getByTestId(IDS.mineEntry)).toHaveText(['Calm']);
  expect(stub.sends).toEqual([{ round: 'round_1', revision: 1, words: ['Calm'] }]);

  hub.pushWordCloud({ ...open, phase: 'closed', revision: 2 });
  await expect(page.getByTestId(IDS.submit)).toBeDisabled();
  await expect(page.getByTestId(IDS.mineEntry)).toHaveText(['Calm']);
});

test('OpenAnswerCard_WhenTheHubStartsANewRound_ShouldClearTheAnswerAndSendForTheNewRound', async ({
  mount,
  page,
}) => {
  const guest = liveOn('open-text');
  const hub = await stubDeckHub(page, { audience: { state: guest.state } });
  await stubApi(page, { '/api/join/482913': { json: guest } });
  const stub = await stubOpenResponses(page, { slide: 3, view: textView('Ship it') });
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await expect(page.getByTestId(IDS.mine)).toHaveText('Ship it');

  hub.pushOpenText({
    ...ROUND,
    type: 'openText',
    slide: 3,
    round: 'round_2',
    number: 2,
    revision: 0,
    guests: 0,
    answers: [],
  });
  await expect(page.getByTestId(IDS.mine)).toHaveCount(0);
  await page.getByTestId(IDS.input).fill('Again');
  await page.getByTestId(IDS.submit).click();

  await expect(page.getByTestId(IDS.mine)).toHaveText('Again');
  expect(stub.sends).toEqual([{ round: 'round_2', revision: 0, text: 'Again' }]);
});

test('WordCard_WhenTheHubOpensTheRoundDuringTheFirstRead_ShouldSendForTheOpenRound', async ({
  mount,
  page,
}) => {
  const guest = liveOn('word-cloud');
  const hub = await stubDeckHub(page, { audience: { state: guest.state } });
  await stubApi(page, { '/api/join/482913': { json: guest } });
  const stub = await stubOpenResponses(page, { slide: 3, view: DRAFT });
  const first = await holdRead(page, 3, 1);
  await mount(<AppRoutes />, { hooksConfig: { route: '/j/482913' } });
  await first.reached;

  hub.pushWordCloud({
    ...ROUND,
    type: 'wordCloud',
    slide: 3,
    revision: 1,
    guests: 1,
    words: [{ text: 'Ready', count: 1, hidden: false }],
  });
  await expect(page.getByTestId(IDS.roomEntry)).toHaveText(['Ready']);
  first.release();

  await expect(page.getByTestId(IDS.submit)).toBeEnabled();
  await page.getByTestId(IDS.input).first().fill('Calm');
  await page.getByTestId(IDS.submit).click();
  await expect(page.getByTestId(IDS.mineEntry)).toHaveText(['Calm']);
  expect(stub.sends).toEqual([{ round: 'round_1', revision: 1, words: ['Calm'] }]);
});

test('WordCard_WhenARefusalRereadAnswersAfterTheSlideChanged_ShouldKeepTheNewSlide', async ({
  mount,
  page,
}) => {
  const next = 'And one more?';
  const stub = await stubOpenResponses(page, { slide: 3, view: wordsView([]) });
  stub.reply(STALE);
  const reread = await holdRead(page, 3, 2);
  await page.route('**/api/join/482913/slides/4/response', (route) =>
    route.fulfill({ json: { ...wordsView([]), question: next } }),
  );
  const card = await mount(<WordCard code="482913" slide={3} status="live" />);
  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);
  await page.getByTestId(IDS.input).first().fill('Calm');
  await page.getByTestId(IDS.submit).click();
  await reread.reached;

  await card.update(<WordCard code="482913" slide={4} status="live" />);
  await expect(page.getByTestId(IDS.question)).toHaveText(next);
  const answered = page.waitForResponse('**/api/join/482913/slides/3/response');
  reread.release();
  await answered;

  await expect(page.getByTestId(IDS.submit)).toBeEnabled();
  await expect(page.getByTestId(IDS.question)).toHaveText(next);
});

test('WordCard_WhenARefusalRereadFailsAfterANewerReadSucceeded_ShouldKeepTheErrorCleared', async ({
  mount,
  page,
}) => {
  const stub = await stubOpenResponses(page, { slide: 3, view: wordsView([]) });
  stub.reply(STALE);
  const reread = await holdRead(page, 3, 2);
  await page.route('**/api/join/482913/slides/4/response', (route) =>
    route.fulfill({ json: { ...wordsView([]), question: 'And one more?' } }),
  );
  const card = await mount(<WordCard code="482913" slide={3} status="live" />);
  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);
  await page.getByTestId(IDS.input).first().fill('Calm');
  await page.getByTestId(IDS.submit).click();
  await reread.reached;
  await expect(page.getByTestId(IDS.error)).toHaveText(OPEN_RESPONSE_ERRORS.stale);

  await card.update(<WordCard code="482913" slide={4} status="live" />);
  await expect(page.getByTestId(IDS.question)).toHaveText('And one more?');
  await card.update(<WordCard code="482913" slide={3} status="live" />);
  await expect(page.getByTestId(IDS.question)).toHaveText(QUESTION);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  const answered = page.waitForResponse('**/api/join/482913/slides/3/response');
  reread.release({ status: 500, json: { error: 'unavailable', message: 'Down.' } });
  await answered;

  await expect(page.getByTestId(IDS.submit)).toBeEnabled();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

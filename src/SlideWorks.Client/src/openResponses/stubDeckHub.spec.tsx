import { test, expect } from '@playwright/experimental-ct-react';
import { stubDeckHub } from '../test-support/stubDeckHub';

const ROUND = {
  phase: 'open',
  round: 'round_1',
  slide: 3,
  number: 1,
  revision: 4,
  revealed: true,
} as const;

test('Stub_WhenTheCountIsNotAnInteger_ShouldRejectTheEvent', async ({ page }) => {
  const hub = await stubDeckHub(page);

  expect(() =>
    hub.pushWordCloud({
      ...ROUND,
      type: 'wordCloud',
      guests: 2,
      words: [{ text: 'Calm', count: 1.5, hidden: false }],
    }),
  ).toThrow('pushWordCloud: count 1.5 is not an integer');
  expect(() => hub.pushOpenText({ ...ROUND, type: 'openText', guests: 2.5, answers: [] })).toThrow(
    'pushOpenText: count 2.5 is not an integer',
  );
});

test('Stub_WhenTheTypeIsMissing_ShouldRejectTheEvent', async ({ page }) => {
  const hub = await stubDeckHub(page);

  expect(() => hub.pushWordCloud({ ...ROUND, guests: 0, words: [] } as never)).toThrow(
    'pushWordCloud: type must be "wordCloud"',
  );
});

test('Stub_WhenAnAudiencePushHasAHiddenEntry_ShouldRejectTheEvent', async ({ page }) => {
  const hub = await stubDeckHub(page);

  expect(() =>
    hub.pushOpenText({
      ...ROUND,
      type: 'openText',
      guests: 1,
      answers: [{ number: 1, text: 'Hidden', hidden: true }],
    }),
  ).toThrow('pushOpenText: an audience push never carries a hidden answer');
});

test('Stub_WhenNothingHasConnected_ShouldThrowFromEveryPush', async ({ page }) => {
  const hub = await stubDeckHub(page);

  expect(() => hub.push({ type: 'deckChanged' })).toThrow('push: no hub connection yet');
  expect(() => hub.pushAudienceEvent({} as never)).toThrow(
    'pushAudienceEvent: no hub connection yet',
  );
  expect(() => hub.pushQuestionTally({} as never)).toThrow(
    'pushQuestionTally: no hub connection yet',
  );
});

test('Stub_WhenNothingHasConnected_ShouldThrowFromEveryPushMethod', async ({ page }) => {
  const hub = await stubDeckHub(page, { audience: { state: { seq: 1 } as never } });
  const anything = {} as never;
  const pushes: Record<string, () => void> = {
    push: () => hub.push({ type: 'deckChanged' }),
    pushAudienceEvent: () => hub.pushAudienceEvent(anything),
    pushPollTally: () => hub.pushPollTally(anything),
    pushPollTallyResults: () => hub.pushPollTally(anything, { target: 'results' }),
    pushWordCloud: () => hub.pushWordCloud({ ...ROUND, type: 'wordCloud', guests: 0, words: [] }),
    pushOpenText: () => hub.pushOpenText({ ...ROUND, type: 'openText', guests: 0, answers: [] }),
    pushActivity: () => hub.pushActivity(null),
    pushQuestionTally: () => hub.pushQuestionTally(anything),
    pushRoom: () => hub.pushRoom(null),
    pushState: () => hub.pushState(anything),
  };
  for (const [name, call] of Object.entries(pushes))
    expect(call, name).toThrow(`${name.replace('Results', '')}: no hub connection yet`);
});

test('Stub_WhenNothingHasConnectedAndThereIsNoAudienceState_ShouldThrowTheConnectionError', async ({
  page,
}) => {
  const hub = await stubDeckHub(page);

  expect(() => hub.pushActivity(null)).toThrow('pushActivity: no hub connection yet');
  expect(() => hub.pushRoom(null)).toThrow('pushRoom: no hub connection yet');
});

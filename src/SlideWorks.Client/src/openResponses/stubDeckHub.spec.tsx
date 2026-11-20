import { test, expect } from '@playwright/experimental-ct-react';
import { stubDeckHub } from '../test-support/stubDeckHub';

const ROUND = { phase: 'open', round: 'round_1', slide: 3, number: 1, revision: 4, revealed: true } as const;

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
  expect(() =>
    hub.pushOpenText({ ...ROUND, type: 'openText', guests: 2.5, answers: [] }),
  ).toThrow('pushOpenText: count 2.5 is not an integer');
});

test('Stub_WhenTheTypeIsMissing_ShouldRejectTheEvent', async ({ page }) => {
  const hub = await stubDeckHub(page);

  expect(() =>
    hub.pushWordCloud({ ...ROUND, guests: 0, words: [] } as never),
  ).toThrow('pushWordCloud: type must be "wordCloud"');
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

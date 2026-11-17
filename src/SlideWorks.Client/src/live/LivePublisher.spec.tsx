import { test, expect } from '@playwright/experimental-ct-react';
import { presentationState } from '../test-support/fixtures/presenting';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { LivePublisher } from './LivePublisher';

test('LivePublisher joins as a presenter and publishes the player position, then each move', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  const component = await mount(
    <LivePublisher deckId="deck-1" slide={3} step={1} onError={() => undefined} />,
  );
  await expect
    .poll(() => hub.updates.at(-1))
    .toEqual({
      slide: 3,
      step: 1,
      black: false,
      skipDeepDives: false,
    });
  await component.update(
    <LivePublisher deckId="deck-1" slide={4} step={0} onError={() => undefined} />,
  );
  await expect.poll(() => hub.updates.at(-1)).toMatchObject({ slide: 4, step: 0 });
});

test('LivePublisher reports a failed publish through onError', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  hub.fail('UpdatePresentation', 'boom');
  const reported: string[] = [];
  await mount(
    <LivePublisher
      deckId="deck-1"
      slide={3}
      step={1}
      onError={(message) => reported.push(message)}
    />,
  );
  await expect.poll(() => reported.length).toBeGreaterThan(0);
  expect(reported[0]).toContain('could not publish');
});

test('LivePublisher reports a refused join through onError', async ({ mount, page }) => {
  await stubDeckHub(page, { presentation: { state: presentationState(), refuse: true } });
  const reported: string[] = [];
  await mount(
    <LivePublisher
      deckId="deck-1"
      slide={3}
      step={1}
      onError={(message) => reported.push(message)}
    />,
  );
  await expect.poll(() => reported.length).toBeGreaterThan(0);
  expect(reported[0]).toContain('refused');
});

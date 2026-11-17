import { test, expect } from '@playwright/experimental-ct-react';
import { presentationState } from '../test-support/fixtures/presenting';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { LivePublisher } from './LivePublisher';

test('LivePublisher joins as a presenter and publishes the player position, then each move', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { presentation: { state: presentationState() } });
  const component = await mount(<LivePublisher deckId="deck-1" slide={3} step={1} />);
  await expect
    .poll(() => hub.updates.at(-1))
    .toEqual({
      slide: 3,
      step: 1,
      black: false,
      skipDeepDives: false,
    });
  await component.update(<LivePublisher deckId="deck-1" slide={4} step={0} />);
  await expect.poll(() => hub.updates.at(-1)).toMatchObject({ slide: 4, step: 0 });
});

import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AudienceHarness } from '../../test-support/AudienceHarness';
import { QuestionsProbe } from '../../test-support/QuestionsProbe';
import { guestView } from '../../test-support/fixtures/guestStates';
import { guestQuestions, qaRoom } from '../../test-support/fixtures/qaStates';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubQuestionsApi } from '../../test-support/stubQuestionsApi';
import type { GuestQuestionsView, QaRoom } from '../types';
import { QuestionsLive } from './QuestionsLive';
import { ReactionsLive } from './ReactionsLive';

// The real providers over the audience connection (the stub hub) and the questions routes (the stub API), with the probe
// showing what useQuestions and usePhoneReactions give. The guest's "view": questions 1, 2, 6 open, 3 answered, 4 its
// own and waiting; mine is [1, 4] at revision 3.
async function guestWithQuestions(page: Page, room: QaRoom, view?: GuestQuestionsView) {
  const live = guestView('live', { room });
  const hub = await stubDeckHub(page, { audience: { state: live.state } });
  const api = await stubQuestionsApi(page, { view: view ?? guestQuestions('view', { room }) });
  return { hub, api, live };
}

const question = (page: Page, n: number) => page.getByTestId(`probe-question-${n}`);

test('a question asked is listed with the asker’s upvote, and mine has it', async ({
  mount,
  page,
}) => {
  const room = qaRoom('open');
  const { api, live } = await guestWithQuestions(page, room);
  await mount(
    <AudienceHarness code="482913" view={live}>
      <QuestionsLive code="482913" room={room} status="live" showing={false}>
        <ReactionsLive code="482913" room={room} status="live">
          <QuestionsProbe />
        </ReactionsLive>
      </QuestionsLive>
    </AudienceHarness>,
  );
  await expect(page.getByTestId('probe-state')).toHaveText('ready');

  await page.getByTestId('probe-text').fill('  How   do you test it? ');
  await page.getByTestId('probe-ask').click();

  await expect(question(page, 7)).toHaveText('7 open 1 yours - anonymous now How do you test it?');
  await expect(page.getByTestId('probe-mine')).toHaveText(
    JSON.stringify({ picks: [1, 4, 7], revision: 4 }),
  );
  await expect(page.getByTestId('probe-asking')).toHaveText('idle');
  expect(api.asks).toEqual([{ text: '  How   do you test it? ', name: null }]);
});

test('an upvote counts at once and is sent, and taking it back sends that', async ({
  mount,
  page,
}) => {
  const room = qaRoom('open');
  const { api, live } = await guestWithQuestions(page, room);
  await mount(
    <AudienceHarness code="482913" view={live}>
      <QuestionsLive code="482913" room={room} status="live" showing={false}>
        <ReactionsLive code="482913" room={room} status="live">
          <QuestionsProbe />
        </ReactionsLive>
      </QuestionsLive>
    </AudienceHarness>,
  );
  await expect(question(page, 2)).toContainText('2 open 9 ');

  await page.getByTestId('probe-vote-2').click();
  await expect(question(page, 2)).toContainText('2 open 10 ');
  // The phone has the PUT's answer before the take-back, so this test is about the two calls, not their race.
  await expect(page.getByTestId('probe-mine')).toHaveText(
    JSON.stringify({ picks: [1, 2, 4], revision: 4 }),
  );
  expect(api.view.mine).toEqual({ picks: [1, 2, 4], revision: 4 });

  await page.getByTestId('probe-vote-2').click();

  await expect(question(page, 2)).toContainText('2 open 9 ');
  await expect.poll(() => api.view.mine).toEqual({ picks: [1, 4], revision: 5 });
  await expect(page.getByTestId('probe-mine')).toHaveText(
    JSON.stringify({ picks: [1, 4], revision: 5 }),
  );
  expect(api.votes.map((v) => v.picks)).toEqual([
    [1, 2, 4],
    [1, 4],
  ]);
  await expect(page.getByTestId('probe-error')).toHaveText('');
});

test('a questionTally on the audience connection moves the votes of its round', async ({
  mount,
  page,
}) => {
  const room = qaRoom('open');
  const { hub, live } = await guestWithQuestions(page, room);
  await mount(
    <AudienceHarness code="482913" view={live}>
      <QuestionsLive code="482913" room={room} status="live" showing={false}>
        <ReactionsLive code="482913" room={room} status="live">
          <QuestionsProbe />
        </ReactionsLive>
      </QuestionsLive>
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');
  await expect(question(page, 6)).toContainText('6 open 3 ');

  hub.pushQuestionTally({
    type: 'questionTally',
    round: room.round!,
    listRevision: room.listRevision,
    votes: [{ number: 6, votes: 8 }],
  });

  await expect(question(page, 6)).toContainText('6 open 8 ');
});

test('asking in a closed room is failed with the code, and nothing is listed', async ({
  mount,
  page,
}) => {
  const room = qaRoom('closed');
  const { api, live } = await guestWithQuestions(page, room);
  await mount(
    <AudienceHarness code="482913" view={live}>
      <QuestionsLive code="482913" room={room} status="live" showing={false}>
        <ReactionsLive code="482913" room={room} status="live">
          <QuestionsProbe />
        </ReactionsLive>
      </QuestionsLive>
    </AudienceHarness>,
  );
  await expect(page.getByTestId('probe-state')).toHaveText('ready');

  await page.getByTestId('probe-text').fill('Too late?');
  await page.getByTestId('probe-ask').click();

  await expect(page.getByTestId('probe-asking')).toHaveText('failed');
  await expect(page.getByTestId('probe-error')).toHaveText('questions_closed');
  expect(api.view.questions.map((q) => q.number)).toEqual([1, 2, 3, 4, 6]);
  await expect(question(page, 7)).toHaveCount(0);
});

test('a limited ask counts retryIn down by whole seconds, then is idle again', async ({
  mount,
  page,
}) => {
  const room = qaRoom('open');
  const { api, live } = await guestWithQuestions(page, room);
  api.queueAsk({ status: 429, json: { error: 'too_many_questions', retryAfter: 2 } });
  await mount(
    <AudienceHarness code="482913" view={live}>
      <QuestionsLive code="482913" room={room} status="live" showing={false}>
        <ReactionsLive code="482913" room={room} status="live">
          <QuestionsProbe />
        </ReactionsLive>
      </QuestionsLive>
    </AudienceHarness>,
  );
  await expect(page.getByTestId('probe-state')).toHaveText('ready');

  await page.getByTestId('probe-text').fill('One more?');
  await page.getByTestId('probe-ask').click();

  await expect(page.getByTestId('probe-asking')).toHaveText('limited');
  await expect(page.getByTestId('probe-retry-in')).toHaveText('2');
  await expect(page.getByTestId('probe-retry-in')).toHaveText('1');
  await expect(page.getByTestId('probe-retry-in')).toHaveText('0');
  await expect(page.getByTestId('probe-asking')).toHaveText('idle');
});

test('a tap past the limit cools the reactions, and a tap while cooling is not sent', async ({
  mount,
  page,
}) => {
  const room = qaRoom('open');
  const { api, live } = await guestWithQuestions(page, room);
  await mount(
    <AudienceHarness code="482913" view={live}>
      <QuestionsLive code="482913" room={room} status="live" showing={false}>
        <ReactionsLive code="482913" room={room} status="live">
          <QuestionsProbe />
        </ReactionsLive>
      </QuestionsLive>
    </AudienceHarness>,
  );
  await expect(page.getByTestId('probe-reactions-enabled')).toHaveText('true');

  for (let tap = 0; tap < 11; tap++) await page.getByTestId('probe-tap-heart').click();

  await expect(page.getByTestId('probe-cooling')).toHaveText('true');
  await expect.poll(() => api.taps.length).toBe(11);
  await page.getByTestId('probe-tap-heart').click();
  await expect(page.getByTestId('probe-cooling')).toHaveText('true');
  expect(api.taps.length).toBe(11);
  await expect(page.getByTestId('probe-reaction-error')).toHaveText('');
});

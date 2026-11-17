import { test, expect } from '@playwright/experimental-ct-react';
import { AudienceHarness } from '../test-support/AudienceHarness';
import { AudiencePollsProbe } from '../test-support/AudiencePollsProbe';
import { guestView } from '../test-support/fixtures/guestStates';
import { pollControl, pollTally } from '../test-support/fixtures/pollStates';
import { stubDeckHub } from '../test-support/stubDeckHub';

const view = guestView('live');

test('every pollTally the hub pushes reaches the poll channel in order, two in one tick included', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await mount(
    <AudienceHarness code="482913" view={view}>
      <AudiencePollsProbe />
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');

  // The results group gets two per change: one with counts hidden, one with them.
  hub.pushPollTally(pollTally({ round: 'r1', answered: 21, counts: null }));
  hub.pushPollTally(pollTally({ round: 'r1', answered: 21, counts: [3, 9, 6, 3] }));
  hub.pushAudienceEvent(pollControl());

  await expect(page.getByTestId('probe-tallies').getByRole('listitem')).toHaveText([
    'r1 21 null',
    'r1 21 [3,9,6,3]',
  ]);
});

test('watchPoll invokes WatchPoll with the code on the same connection, and again after a reconnect', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  hub.setPollWatchable(true);
  await mount(
    <AudienceHarness code="482913" view={view}>
      <AudiencePollsProbe />
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');

  await page.getByTestId('probe-watch').click();
  await expect(page.getByTestId('probe-watch-result')).toHaveText('watching');
  expect(hub.pollWatches).toEqual(['482913']);
  expect(hub.watchingPoll).toBe(true);

  hub.drop();
  await expect.poll(() => hub.audienceJoins.length).toBe(2);
  await expect(page.getByTestId('harness-status')).toHaveText('live');
  expect(hub.watchingPoll).toBe(false);
  await page.getByTestId('probe-watch').click();

  await expect.poll(() => hub.pollWatches).toEqual(['482913', '482913']);
  await expect.poll(() => hub.watchingPoll).toBe(true);
  hub.pushPollTally(pollTally({ round: 'r1', answered: 5, counts: [1, 2, 1, 1] }), {
    target: 'results',
  });
  await expect(page.getByTestId('probe-tallies').getByRole('listitem')).toHaveText([
    'r1 5 [1,2,1,1]',
  ]);
});

test('the hub stub refuses WatchPoll with not_answered until the guest may watch', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await mount(
    <AudienceHarness code="482913" view={view}>
      <AudiencePollsProbe />
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');

  await page.getByTestId('probe-watch').click();

  await expect(page.getByTestId('probe-watch-result')).toHaveText(/HubException: not_answered$/);
  expect(hub.pollWatches).toEqual(['482913']);
  expect(hub.watchingPoll).toBe(false);
});

test('a results tally reaches only a connection that watched, and a drop forgets the watch', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  hub.setPollWatchable(true);
  await mount(
    <AudienceHarness code="482913" view={view}>
      <AudiencePollsProbe />
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');
  const tallies = page.getByTestId('probe-tallies').getByRole('listitem');

  hub.pushPollTally(pollTally({ round: 'r1', answered: 1, counts: [1, 0, 0, 0] }), {
    target: 'results',
  });
  hub.pushPollTally(pollTally({ round: 'r1', answered: 1, counts: null }));
  await expect(tallies).toHaveText(['r1 1 null']);

  await page.getByTestId('probe-watch').click();
  await expect(page.getByTestId('probe-watch-result')).toHaveText('watching');
  hub.pushPollTally(pollTally({ round: 'r1', answered: 2, counts: [1, 1, 0, 0] }), {
    target: 'results',
  });
  await expect(tallies).toHaveText(['r1 1 null', 'r1 2 [1,1,0,0]']);

  hub.drop();
  await expect.poll(() => hub.audienceJoins.length).toBe(2);
  await expect(page.getByTestId('harness-status')).toHaveText('live');
  hub.pushPollTally(pollTally({ round: 'r1', answered: 3, counts: [2, 1, 0, 0] }), {
    target: 'results',
  });
  hub.pushPollTally(pollTally({ round: 'r1', answered: 3, counts: null }));

  await expect(tallies).toHaveText(['r1 1 null', 'r1 2 [1,1,0,0]', 'r1 3 null']);
});

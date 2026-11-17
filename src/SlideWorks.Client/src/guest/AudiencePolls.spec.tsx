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
  await mount(
    <AudienceHarness code="482913" view={view}>
      <AudiencePollsProbe />
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');

  await page.getByTestId('probe-watch').click();
  await expect(page.getByTestId('probe-watch-result')).toHaveText('watching');
  expect(hub.pollWatches).toEqual(['482913']);

  hub.drop();
  await expect.poll(() => hub.audienceJoins.length).toBe(2);
  await expect(page.getByTestId('harness-status')).toHaveText('live');
  await page.getByTestId('probe-watch').click();

  await expect.poll(() => hub.pollWatches).toEqual(['482913', '482913']);
  // The tally still reaches the channel on the new connection.
  hub.pushPollTally(pollTally({ round: 'r1', answered: 5, counts: [1, 2, 1, 1] }));
  await expect(page.getByTestId('probe-tallies').getByRole('listitem')).toHaveText([
    'r1 5 [1,2,1,1]',
  ]);
});

test('a refused WatchPoll rejects with the hub’s not_answered', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { audience: { state: view.state } });
  await mount(
    <AudienceHarness code="482913" view={view}>
      <AudiencePollsProbe />
    </AudienceHarness>,
  );
  await expect(page.getByTestId('harness-status')).toHaveText('live');
  hub.fail('WatchPoll', 'not_answered');

  await page.getByTestId('probe-watch').click();

  await expect(page.getByTestId('probe-watch-result')).toHaveText(/HubException: not_answered$/);
  expect(hub.pollWatches).toEqual(['482913']);
});

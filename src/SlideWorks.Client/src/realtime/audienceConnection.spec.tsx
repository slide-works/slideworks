import { test, expect } from '@playwright/experimental-ct-react';
import type { AudienceState } from '../guest/audienceEvents';
import { audienceState, guestView } from '../test-support/fixtures/guestStates';
import { stubDeckHub, type StubTransport } from '../test-support/stubDeckHub';
import { AudienceProbe } from '../test-support/AudienceProbe';

// The state JoinAudience answers with: seq 42, as GET /api/join sends it. The event sample that follows is seq 43.
const live: AudienceState = guestView('live').state;

for (const transport of ['WebSockets', 'ServerSentEvents', 'LongPolling'] as StubTransport[]) {
  test(`${transport}: joins the audience and receives an audienceEvent`, async ({
    mount,
    page,
  }) => {
    const hub = await stubDeckHub(page, { audience: { state: live }, transports: [transport] });
    await mount(<AudienceProbe code="482913" />);
    await expect(page.getByTestId('probe-state')).toHaveText('seq 42');
    expect(hub.audienceJoins).toEqual(['482913']);
    expect(hub.transportsUsed).toEqual([transport]);

    hub.pushAudienceEvent(audienceState());
    await expect(page.getByTestId('probe-state')).toHaveText('seq 43');
  });
}

for (const transport of ['WebSockets', 'ServerSentEvents', 'LongPolling'] as StubTransport[]) {
  test(`${transport}: a dropped connection reconnects and lands on the pushed state`, async ({
    mount,
    page,
  }) => {
    const hub = await stubDeckHub(page, { audience: { state: live }, transports: [transport] });
    await mount(<AudienceProbe code="482913" />);
    await expect(page.getByTestId('probe-state')).toHaveText('seq 42');

    // The state moves on while the guest is away; only a rejoin after the drop can reach seq 43.
    hub.setAudienceState(audienceState());
    hub.drop();
    await expect(page.getByTestId('probe-status')).toHaveText('live');
    await expect(page.getByTestId('probe-state')).toHaveText('seq 43');
    expect(hub.audienceJoins).toEqual(['482913', '482913']);
    expect(hub.transportsUsed).toEqual([transport, transport]);

    hub.pushAudienceEvent(audienceState({ seq: 44 }));
    await expect(page.getByTestId('probe-state')).toHaveText('seq 44');
  });
}

test('with every transport offered the connection uses WebSockets', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { audience: { state: live } });
  await mount(<AudienceProbe code="482913" />);
  await expect(page.getByTestId('probe-state')).toHaveText('seq 42');
  expect(hub.transportsUsed).toEqual(['WebSockets']);
});

test('an unknown code is refused with the hub error', async ({ mount, page }) => {
  await stubDeckHub(page, { audience: { state: live, refuse: 'no_session' } });
  await mount(<AudienceProbe code="000000" />);
  await expect(page.getByTestId('probe-error')).toContainText('no_session');
});

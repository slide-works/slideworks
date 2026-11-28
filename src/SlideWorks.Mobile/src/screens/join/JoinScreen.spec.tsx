import { GUEST_HEADER } from '@slideworks/core/guest';
import type { HubConnectionOptions } from '@slideworks/core/realtime/hub';
import { act, screen, waitFor } from 'expo-router/testing-library';
import { redirectSystemPath } from '../../../app/+native-intent';
import { guestKey } from '../../guest/guestStore';
import { HOSTED_SERVER, links } from '../../links';
import { anAudienceState, aJoinSummary } from '../../test-support/builders';
import { fakeApi, type FakeRequest } from '../../test-support/fakeApi';
import * as fakeHubModule from '../../test-support/fakeHub';
import { fakeHub } from '../../test-support/fakeHub';
import { mockStorage } from '../../test-support/nativeMocks';
import { renderApp } from '../../test-support/renderApp';
import { signedInApi } from '../../test-support/signedInApi';
import { joinTestIds } from './testIds';

const code = '123456';
const issuedId = '0123456789abcdef0123456789abcdef';
const heldId = 'fedcba9876543210fedcba9876543210';
const otherServer = 'http://10.0.2.2:5510';
const joinRoute = `GET /api/join/${code}`;

const live = (index: number, seq = index) =>
  anAudienceState({ phase: 'live', seq, current: { index, count: 12 } });
const lobby = anAudienceState({ phase: 'lobby', seq: 1, current: null });

const guestHeader = (request: FakeRequest) =>
  Object.entries(request.headers).find(
    ([name]) => name.toLowerCase() === GUEST_HEADER.toLowerCase(),
  )?.[1];
const authorization = (request: FakeRequest) =>
  Object.entries(request.headers).find(([name]) => name.toLowerCase() === 'authorization')?.[1];

// The server's join (K7, D10): a request with no valid guest id gets a new one in the header; one with an id gets none.
function joinAnswer(state = live(3)) {
  return (request: FakeRequest) =>
    guestHeader(request)
      ? aJoinSummary({ deckTitle: 'Ship smaller', state })
      : {
          status: 200,
          body: aJoinSummary({ deckTitle: 'Ship smaller', state }),
          headers: { [GUEST_HEADER]: issuedId },
        };
}

const storedId = (origin: string) => JSON.parse(mockStorage.kv.get(guestKey(origin)) ?? '{}').id;

describe('Join', () => {
  it('stores the issued guest id and sends it next time', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    let idAtBuild: string | null | undefined;
    const build = fakeHubModule.fakeHubFactory;
    const spy = jest
      .spyOn(fakeHubModule, 'fakeHubFactory')
      .mockImplementation((options: HubConnectionOptions) => {
        if (options.auth.kind === 'guest') idAtBuild = options.auth.guestId();
        return build(options);
      });
    const api = fakeApi({ [joinRoute]: joinAnswer() });
    const first = await renderApp({ url: links.join(code), api });

    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();
    expect(guestHeader(api.callsTo(joinRoute)[0])).toBeUndefined();
    expect(storedId(HOSTED_SERVER)).toBe(issuedId);
    // The hub reads the id once, when it is built: the issued id was already stored then.
    expect(idAtBuild).toBe(issuedId);
    expect(hub.invokes).toEqual([{ method: 'JoinAudience', args: [code] }]);
    await first.unmount();
    spy.mockRestore();

    fakeHub().respond('JoinAudience', live(3));
    await renderApp({ url: links.join(code), api });
    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();
    expect(guestHeader(api.callsTo(joinRoute)[1])).toBe(issuedId);
    expect(storedId(HOSTED_SERVER)).toBe(issuedId);
  });

  it('shows the title and follows the slide', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer() }) });

    expect(await screen.findByTestId(joinTestIds.title)).toHaveTextContent('Ship smaller');
    expect(await screen.findByTestId(joinTestIds.slide)).toHaveTextContent(/\b3\b.*\b12\b/);

    await act(async () => hub.emit('audienceEvent', live(4)));
    expect(screen.getByTestId(joinTestIds.slide)).toHaveTextContent(/\b4\b.*\b12\b/);

    // An older state arriving late never moves the slide back.
    await act(async () => hub.emit('audienceEvent', live(2)));
    expect(screen.getByTestId(joinTestIds.slide)).toHaveTextContent(/\b4\b.*\b12\b/);

    // A dropped connection rejoins and lands on the state that answers.
    hub.respond('JoinAudience', live(7));
    await act(async () => hub.drop());
    expect(screen.getByTestId(joinTestIds.offline)).toBeOnTheScreen();
    await act(async () => hub.restore());
    expect(await screen.findByTestId(joinTestIds.slide)).toHaveTextContent(/\b7\b.*\b12\b/);
    expect(screen.queryByTestId(joinTestIds.offline)).toBeNull();
    expect(hub.invokes.map((invoke) => invoke.method)).toEqual(['JoinAudience', 'JoinAudience']);
  });

  it('keeps a newer pushed state over a late JoinAudience answer', async () => {
    const hub = fakeHub();
    let answer: (state: unknown) => void = () => undefined;
    hub.respond('JoinAudience', () => new Promise((resolve) => (answer = resolve)));
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer(live(3)) }) });
    await waitFor(() => expect(hub.invokes).toHaveLength(1));

    // A newer state is pushed while the call is pending; the answer, read before it, arrives late.
    await act(async () => hub.emit('audienceEvent', live(5)));
    expect(screen.getByTestId(joinTestIds.slide)).toHaveTextContent(/\b5\b.*\b12\b/);
    await act(async () => answer(live(4)));

    expect(screen.getByTestId(joinTestIds.slide)).toHaveTextContent(/\b5\b.*\b12\b/);

    // The next rejoin lands on its answer whatever its seq (the server may have restarted).
    hub.respond('JoinAudience', live(2));
    await act(async () => hub.drop());
    await act(async () => hub.restore());
    await waitFor(() =>
      expect(screen.getByTestId(joinTestIds.slide)).toHaveTextContent(/\b2\b.*\b12\b/),
    );
  });

  it('builds the guest hub with all three transports and the web back-off', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer() }) });
    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();

    // HttpTransportType.WebSockets | ServerSentEvents | LongPolling.
    expect(hub.options?.transport).toBe(1 | 2 | 4);
    const delays = [0, 1, 2, 3, 4, 5].map((count) => hub.options?.nextRetryDelayMs(count));
    expect(delays).toEqual([0, 2000, 5000, 10000, 30000, null]);
  });

  it('lobby then live', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', lobby);
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer(lobby) }) });

    expect(await screen.findByTestId(joinTestIds.lobby)).toBeOnTheScreen();
    expect(screen.queryByTestId(joinTestIds.slide)).toBeNull();

    // Going live keeps the lobby's seq: a new phase is not a repeat.
    await act(async () =>
      hub.emit(
        'audienceEvent',
        anAudienceState({ phase: 'live', seq: 1, current: { index: 1, count: 12 } }),
      ),
    );
    expect(screen.getByTestId(joinTestIds.slide)).toHaveTextContent(/\b1\b.*\b12\b/);
    expect(screen.queryByTestId(joinTestIds.lobby)).toBeNull();
  });

  it('ended', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer() }) });
    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();

    await act(async () => hub.emit('audienceEvent', { type: 'audienceEnded' }));

    expect(screen.getByTestId(joinTestIds.ended)).toBeOnTheScreen();
    expect(screen.queryByTestId(joinTestIds.slide)).toBeNull();
    expect(hub.state).toBe('Disconnected');
  });

  it('ended when the join already says so, without a hub', async () => {
    const ended = anAudienceState({ phase: 'ended', seq: 9, current: null });
    const hub = fakeHub();
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer(ended) }) });

    expect(await screen.findByTestId(joinTestIds.ended)).toBeOnTheScreen();
    expect(hub.builds).toBe(0);
  });

  it('no session message', async () => {
    const hub = fakeHub();
    const api = fakeApi({ [joinRoute]: { status: 404, body: { error: 'no_session' } } });
    await renderApp({ url: links.join(code), api });

    expect(await screen.findByTestId(joinTestIds.noSession)).toHaveTextContent(/123 456/);
    expect(mockStorage.kv.get(guestKey(HOSTED_SERVER))).toBeUndefined();
    expect(hub.builds).toBe(0);
  });

  it('too many attempts message', async () => {
    const hub = fakeHub();
    const api = fakeApi({ [joinRoute]: { status: 429, body: { error: 'too_many_attempts' } } });
    await renderApp({ url: links.join(code), api });

    expect(await screen.findByTestId(joinTestIds.tooManyAttempts)).toBeOnTheScreen();
    expect(screen.queryByTestId(joinTestIds.noSession)).toBeNull();
    expect(mockStorage.kv.get(guestKey(HOSTED_SERVER))).toBeUndefined();
    expect(hub.builds).toBe(0);
  });

  it('a hub no_session refusal shows the no session message', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', () => {
      throw new Error(
        "An unexpected error occurred invoking 'JoinAudience' on the server. HubException: no_session",
      );
    });
    await renderApp({ url: links.join(code), api: fakeApi({ [joinRoute]: joinAnswer() }) });

    expect(await screen.findByTestId(joinTestIds.noSession)).toBeOnTheScreen();
  });

  it('never sends the bearer on guest requests', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    const api = signedInApi({ [joinRoute]: joinAnswer() });
    await renderApp({
      url: links.join(code),
      api,
      signedIn: { origin: HOSTED_SERVER, email: 'sam@example.com', secret: 'swd_secret' },
      guest: { origin: HOSTED_SERVER, id: heldId },
    });

    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();
    const join = api.callsTo(joinRoute)[0];
    expect(join.origin).toBe(HOSTED_SERVER);
    expect(guestHeader(join)).toBe(heldId);
    expect(authorization(join)).toBeUndefined();
    expect(hub.auth).toMatchObject({ kind: 'guest', origin: HOSTED_SERVER });
    expect(hub.auth?.kind === 'guest' && hub.auth.guestId()).toBe(heldId);
    expect(screen.queryByTestId(joinTestIds.server)).toBeNull();
  });

  it('guest id only goes to its own origin', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    const api = fakeApi({ [joinRoute]: joinAnswer() });
    await renderApp({
      url: links.join(code, otherServer),
      api,
      guest: { origin: HOSTED_SERVER, id: heldId },
    });

    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();
    const join = api.callsTo(joinRoute)[0];
    expect(join.origin).toBe(otherServer);
    expect(guestHeader(join)).toBeUndefined();
    expect(storedId(otherServer)).toBe(issuedId);
    expect(storedId(HOSTED_SERVER)).toBe(heldId);
    expect(hub.auth).toMatchObject({ kind: 'guest', origin: otherServer });
    expect(hub.auth?.kind === 'guest' && hub.auth.guestId()).toBe(issuedId);
  });
});

describe('deep links', () => {
  it('slideworks.live/<code> opens the join screen on the hosted server', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    const api = fakeApi({ [joinRoute]: joinAnswer() });
    const url = redirectSystemPath({ path: `https://slideworks.live/${code}`, initial: true });
    await renderApp({ url: url ?? links.home(), api });

    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();
    expect(api.callsTo(joinRoute)[0].origin).toBe(HOSTED_SERVER);
    expect(hub.auth).toMatchObject({ kind: 'guest', origin: HOSTED_SERVER });
  });

  it('slideworks://join/<code>?server= opens it on that server', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', live(3));
    const api = fakeApi({ [joinRoute]: joinAnswer() });
    const url = redirectSystemPath({
      path: `slideworks://join/${code}?server=${encodeURIComponent(otherServer)}`,
      initial: true,
    });
    await renderApp({ url: url ?? links.home(), api });

    expect(await screen.findByTestId(joinTestIds.slide)).toBeOnTheScreen();
    expect(api.callsTo(joinRoute)[0].origin).toBe(otherServer);
    expect(hub.auth).toMatchObject({ kind: 'guest', origin: otherServer });
    expect(screen.getByTestId(joinTestIds.server)).toHaveTextContent('10.0.2.2:5510');
  });
});

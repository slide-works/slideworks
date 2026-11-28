import { act, screen } from '@testing-library/react-native';
import { platform } from '@slideworks/core/platform';
import { guestKey } from '../../guest/guestStore';
import { SESSION_KEY } from '../../session/sessionStore';
import { aPresentationJoined } from '../../test-support/builders';
import { fakeHub } from '../../test-support/fakeHub';
import { DEVICE_NAME, mockStorage } from '../../test-support/nativeMocks';
import { renderScreen } from '../../test-support/renderScreen';
import { signedInApi } from '../../test-support/signedInApi';
import { talkTestIds } from './testIds';
import { TalkScreen } from './TalkScreen';

const origin = 'https://slides.example.org';
const deckId = '7Kq2abcdEFGH';
const presenterPath = `GET /api/decks/${deckId}/presenter`;

function signIn() {
  mockStorage.secure.set(SESSION_KEY, JSON.stringify({ origin, secret: 'swd_secret' }));
}

async function renderTalk() {
  signIn();
  const api = signedInApi({ [presenterPath]: { title: 'Ship smaller' } });
  const hub = fakeHub();
  hub.respond('JoinPresentation', aPresentationJoined({ slide: 3 }));
  const rendered = await renderScreen(<TalkScreen deckId={deckId} />);
  return { api, hub, ...rendered };
}

describe('Talk', () => {
  it('shows the title and the current slide', async () => {
    const { hub } = await renderTalk();

    expect(await screen.findByTestId(talkTestIds.title)).toHaveTextContent('Ship smaller');
    expect(await screen.findByTestId(talkTestIds.slide)).toHaveTextContent(/\b3\b/);
    const [join] = hub.invokes;
    expect(join.method).toBe('JoinPresentation');
    const [joinedDeck, clientId, role, label, timeZone, resume] = join.args;
    expect({ joinedDeck, role, label, resume }).toEqual({
      joinedDeck: deckId,
      role: 'phone',
      label: DEVICE_NAME,
      resume: null,
    });
    expect(clientId).toEqual(expect.stringMatching(/\S{16,}/));
    expect(timeZone).toBe(Intl.DateTimeFormat().resolvedOptions().timeZone);
  });

  it('slide number follows presentation events', async () => {
    const { hub } = await renderTalk();
    await screen.findByTestId(talkTestIds.slide);

    await act(async () =>
      hub.emit('presentationState', { ...aPresentationJoined().state, slide: 7 }),
    );

    expect(screen.getByTestId(talkTestIds.slide)).toHaveTextContent(/\b7\b/);
  });

  it('the hub gets the bearer for the signed-in origin only', async () => {
    mockStorage.kv.set(
      guestKey(origin),
      JSON.stringify({ id: '0123456789abcdef0123456789abcdef', lastUsedAt: Date.now() }),
    );
    const { hub, runtime } = await renderTalk();
    await screen.findByTestId(talkTestIds.slide);

    expect(hub.auth?.kind).toBe('bearer');
    const auth = hub.auth as Extract<typeof hub.auth, { kind: 'bearer' }>;
    expect(platform().apiOrigin()).toBe(origin);
    expect(auth.secret()).toBe('swd_secret');

    expect(hub.auth).not.toHaveProperty('guestId');
    // Read at each (re)connect, so a signed-out app sends nothing.
    runtime.session.clearSecret();
    expect(auth.secret()).toBeNull();
  });

  it('shows reconnecting while the hub is down', async () => {
    const { hub } = await renderTalk();
    await screen.findByTestId(talkTestIds.slide);
    expect(screen.queryByTestId(talkTestIds.reconnecting)).toBeNull();

    await act(async () => hub.drop());
    expect(screen.getByTestId(talkTestIds.reconnecting)).toBeOnTheScreen();

    hub.respond('JoinPresentation', aPresentationJoined({ slide: 9 }));
    await act(async () => hub.restore());

    expect(screen.queryByTestId(talkTestIds.reconnecting)).toBeNull();
    expect(screen.getByTestId(talkTestIds.slide)).toHaveTextContent(/\b9\b/);
    expect(hub.invokes.filter((call) => call.method === 'JoinPresentation')).toHaveLength(2);
  });

  describe('not yours message', () => {
    it('when the deck answers 404', async () => {
      signIn();
      signedInApi({ [presenterPath]: { status: 404, body: { error: 'Not found' } } });
      fakeHub().respond('JoinPresentation', aPresentationJoined());
      await renderScreen(<TalkScreen deckId={deckId} />);

      expect(await screen.findByTestId(talkTestIds.notYours)).toBeOnTheScreen();
      expect(screen.queryByTestId(talkTestIds.slide)).toBeNull();
    });

    it('when the hub refuses the join with DECK_NOT_FOUND', async () => {
      signIn();
      signedInApi({ [presenterPath]: { title: 'Ship smaller' } });
      fakeHub().respond('JoinPresentation', () => {
        throw new Error(
          "An unexpected error occurred invoking 'JoinPresentation' on the server. HubException: Deck not found",
        );
      });
      await renderScreen(<TalkScreen deckId={deckId} />);

      expect(await screen.findByTestId(talkTestIds.notYours)).toBeOnTheScreen();
      expect(screen.queryByTestId(talkTestIds.title)).toBeNull();
    });
  });
});

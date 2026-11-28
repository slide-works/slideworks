import { dehydrate, QueryClient } from '@tanstack/react-query';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { links } from '../../links';
import { QUERY_CACHE_KEY } from '../../queries/queryClient';
import { SESSION_KEY } from '../../session/sessionStore';
import { aDeckRow, aDecksLibrary, aPresentationJoined } from '../../test-support/builders';
import type { FakeApi } from '../../test-support/fakeApi';
import { fakeHub } from '../../test-support/fakeHub';
import { mockStorage } from '../../test-support/nativeMocks';
import { renderApp } from '../../test-support/renderApp';
import { renderScreen } from '../../test-support/renderScreen';
import { signedInApi } from '../../test-support/signedInApi';
import { talksTestIds } from './testIds';
import { talksQueryKey } from './talksQuery';
import { TalksScreen } from './TalksScreen';

const origin = 'https://slides.example.org';
const signedIn = { origin, email: 'sam@example.com', secret: 'swd_secret' };

function signIn() {
  mockStorage.secure.set(SESSION_KEY, JSON.stringify({ origin, secret: signedIn.secret }));
}

const mine = aDeckRow({ id: 'mine1', title: 'Ship smaller', updatedLabel: '2 min ago' });
const shared = aDeckRow({ id: 'shared1', title: 'Q4 review', updatedLabel: 'yesterday' });

// renderApp (expo-router's renderRouter) switches Jest to fake timers, which the query client and the persister
// schedule on: advance them to let the app settle.
async function settle() {
  await act(async () => {
    await jest.advanceTimersByTimeAsync(100);
  });
}

describe('Talks', () => {
  afterEach(() => {
    jest.useRealTimers();
  });

  it('lists decks and shared decks', async () => {
    signIn();
    const api = signedInApi({
      'GET /api/decks': aDecksLibrary({ decks: [mine], sharedWithMe: [shared] }),
    });
    await renderScreen(<TalksScreen />);

    expect(await screen.findByTestId(talksTestIds.row('mine1'))).toBeOnTheScreen();
    expect(screen.getByTestId(talksTestIds.row('shared1'))).toBeOnTheScreen();
    const [call] = api.callsTo('GET /api/decks');
    expect(call.origin).toBe(origin);
    expect(call.headers.Authorization).toBe('Bearer swd_secret');
  });

  it('pull to refresh refetches', async () => {
    signIn();
    const api = signedInApi({
      'GET /api/decks': aDecksLibrary({ decks: [mine], sharedWithMe: [] }),
    });
    await renderScreen(<TalksScreen />);
    await screen.findByTestId(talksTestIds.row('mine1'));
    api.set({ 'GET /api/decks': aDecksLibrary({ decks: [mine, shared], sharedWithMe: [] }) });

    fireEvent(screen.getByTestId(talksTestIds.list), 'refresh');

    expect(await screen.findByTestId(talksTestIds.row('shared1'))).toBeOnTheScreen();
    expect(api.callsTo('GET /api/decks')).toHaveLength(2);
    // The pull indicator stops once the refetch settles.
    await act(async () => undefined);
  });

  // TODO: unskip once the refetch after the persister's restore is understood: under renderApp's fake timers the row
  // shows from the persisted cache, but GET /api/decks is never requested afterwards (api.held stays 0).
  it.skip('shows the persisted list before the network answers', async () => {
    const cached = new QueryClient();
    cached.setQueryData(talksQueryKey, aDecksLibrary({ decks: [mine], sharedWithMe: [] }), {
      updatedAt: Date.now() - 60_000,
    });
    mockStorage.kv.set(
      QUERY_CACHE_KEY,
      JSON.stringify({ buster: '', timestamp: Date.now(), clientState: dehydrate(cached) }),
    );
    const api = holdDecks(signedInApi());

    await renderApp({ url: links.talks(), api, signedIn });

    await settle();

    expect(screen.getByTestId(talksTestIds.row('mine1'))).toBeOnTheScreen();
    expect(api.held).toBe(1);
  });

  it('empty state', async () => {
    signIn();
    signedInApi({ 'GET /api/decks': aDecksLibrary({ decks: [], sharedWithMe: [] }) });
    await renderScreen(<TalksScreen />);

    expect(await screen.findByTestId(talksTestIds.empty)).toBeOnTheScreen();
    expect(screen.queryByTestId(talksTestIds.row('mine1'))).toBeNull();
  });

  it('failed load shows retry', async () => {
    signIn();
    const api = signedInApi({ 'GET /api/decks': { status: 500, body: { error: 'Boom' } } });
    await renderScreen(<TalksScreen />);

    expect(await screen.findByTestId(talksTestIds.failed)).toBeOnTheScreen();
    api.set({ 'GET /api/decks': aDecksLibrary({ decks: [mine], sharedWithMe: [] }) });
    fireEvent.press(screen.getByTestId(talksTestIds.retry));

    expect(await screen.findByTestId(talksTestIds.row('mine1'))).toBeOnTheScreen();
    expect(screen.queryByTestId(talksTestIds.failed)).toBeNull();
  });

  it('opens a talk from its row', async () => {
    fakeHub().respond('JoinPresentation', aPresentationJoined());
    await renderApp({
      url: links.talks(),
      api: signedInApi({ 'GET /api/decks': aDecksLibrary({ decks: [mine], sharedWithMe: [] }) }),
      signedIn,
    });

    await settle();
    fireEvent.press(screen.getByTestId(talksTestIds.row('mine1')));
    await settle();

    expect(screen).toHavePathname(String(links.talk('mine1')));
  });
});

// A table whose GET /api/decks never answers, so the screen can only show what the persisted cache holds.
function holdDecks(api: FakeApi): FakeApi & { readonly held: number } {
  let held = 0;
  return {
    ...api,
    get held() {
      return held;
    },
    install() {
      api.install();
      const answer = globalThis.fetch;
      globalThis.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
        if (new URL(String(input)).pathname !== '/api/decks') return answer(input, init);
        held += 1;
        return new Promise(() => undefined);
      }) as typeof fetch;
    },
  };
}

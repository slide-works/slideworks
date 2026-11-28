import { fireEvent, screen } from 'expo-router/testing-library';
import { links } from '../../links';
import { anAudienceState, aJoinSummary } from '../../test-support/builders';
import { fakeApi } from '../../test-support/fakeApi';
import { fakeHub } from '../../test-support/fakeHub';
import { renderApp } from '../../test-support/renderApp';
import { signedInApi } from '../../test-support/signedInApi';
import { joinTestIds } from '../join/testIds';
import { talksTestIds } from '../talks/testIds';
import { homeTestIds } from './testIds';

const signedIn = {
  origin: 'https://slideworks.app',
  email: 'sam@example.com',
  secret: 'swd_secret',
};

async function enter(code: string) {
  await fireEvent.changeText(screen.getByTestId(homeTestIds.codeInput), code);
  await fireEvent.press(screen.getByTestId(homeTestIds.join));
}

describe('Home', () => {
  it('join goes to the talk', async () => {
    const hub = fakeHub();
    hub.respond('JoinAudience', anAudienceState());
    const api = fakeApi({ 'GET /api/join/123456': aJoinSummary() });
    await renderApp({ api });

    await enter('123456');

    expect(await screen.findByTestId(joinTestIds.root)).toBeOnTheScreen();
    expect(screen).toHavePathname(String(links.join('123456')));
    expect(api.callsTo('GET /api/join/123456')[0].origin).toBe('https://slideworks.app');
  });

  it("rejects a code that isn't six digits", async () => {
    const api = fakeApi();
    await renderApp({ api });

    for (const code of ['12345', '12345a', '']) {
      await enter(code);
      expect(screen.getByTestId(homeTestIds.codeInvalid)).toBeOnTheScreen();
      expect(screen.getByTestId(homeTestIds.root)).toBeOnTheScreen();
    }
    expect(screen).toHavePathname(String(links.home()));
    expect(api.calls()).toEqual([]);

    await fireEvent.changeText(screen.getByTestId(homeTestIds.codeInput), '1');
    expect(screen.queryByTestId(homeTestIds.codeInvalid)).toBeNull();
  });

  it('shows Sign in when signed out', async () => {
    await renderApp({ api: fakeApi() });

    expect(screen.getByTestId(homeTestIds.signIn)).toBeOnTheScreen();
    expect(screen.queryByTestId(homeTestIds.talks)).toBeNull();
  });

  it('shows Talks when signed in', async () => {
    await renderApp({ api: signedInApi(), signedIn });

    expect(screen.queryByTestId(homeTestIds.signIn)).toBeNull();
    await fireEvent.press(screen.getByTestId(homeTestIds.talks));

    expect(await screen.findByTestId(talksTestIds.root)).toBeOnTheScreen();
  });
});

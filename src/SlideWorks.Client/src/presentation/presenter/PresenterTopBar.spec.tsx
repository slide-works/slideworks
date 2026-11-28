import { test, expect } from '@playwright/experimental-ct-react';
import { presentationState, presenterView } from '../../test-support/fixtures/presenting';
import type { PresentationState, TimerCommand } from '../types';
import { PresenterTopBar } from './PresenterTopBar';

test('a passed liveChip renders first in the right-hand group', async ({ mount, page }) => {
  await mount(
    <PresenterTopBar
      view={presenterView()}
      state={presentationState()}
      elapsedMs={760000}
      clockMinutes={872}
      onTimer={() => {}}
      onReopenAudience={() => {}}
      liveChip={<span data-testid="probe-live-chip">Live: 12 following</span>}
    />,
  );
  const wrapper = page.getByTestId('presenter-live-chip');
  await expect(wrapper.getByTestId('probe-live-chip')).toHaveText('Live: 12 following');
  const firstInGroup = await wrapper.evaluate((el) => el.parentElement?.firstElementChild === el);
  expect(firstInGroup).toBe(true);
});

test('without liveChip the wrapper is not rendered', async ({ mount, page }) => {
  await mount(
    <PresenterTopBar
      view={presenterView()}
      state={presentationState()}
      elapsedMs={0}
      clockMinutes={0}
      onTimer={() => {}}
      onReopenAudience={() => {}}
    />,
  );
  await expect(page.getByTestId('presenter-top-bar')).toBeVisible();
  await expect(page.getByTestId('presenter-live-chip')).toHaveCount(0);
});

test('PresenterTopBar: a passed readingIn renders just before the live chip', async ({
  mount,
  page,
}) => {
  await mount(
    <PresenterTopBar
      view={presenterView()}
      state={presentationState()}
      elapsedMs={0}
      clockMinutes={0}
      onTimer={() => {}}
      onReopenAudience={() => {}}
      readingIn={<span data-testid="probe-reading-in">Reading in EN 31</span>}
      liveChip={<span data-testid="probe-live-chip">Live: 12 following</span>}
    />,
  );
  const wrapper = page.getByTestId('presenter-reading-in');
  await expect(wrapper.getByTestId('probe-reading-in')).toHaveText('Reading in EN 31');
  const beforeLiveChip = await wrapper.evaluate(
    (el) => el.nextElementSibling?.getAttribute('data-testid') === 'presenter-live-chip',
  );
  expect(beforeLiveChip).toBe(true);
});

test('PresenterTopBar: without readingIn its wrapper is not rendered', async ({ mount, page }) => {
  await mount(
    <PresenterTopBar
      view={presenterView()}
      state={presentationState()}
      elapsedMs={0}
      clockMinutes={0}
      onTimer={() => {}}
      onReopenAudience={() => {}}
      liveChip={<span data-testid="probe-live-chip">Live: 12 following</span>}
    />,
  );
  await expect(page.getByTestId('presenter-live-chip')).toBeVisible();
  await expect(page.getByTestId('presenter-reading-in')).toHaveCount(0);
});

const MIN = 60000;

function bar(
  props: {
    elapsedMs?: number;
    state?: PresentationState;
    onTimer?: (c: TimerCommand) => void;
    onReopen?: () => void;
  } = {},
) {
  return (
    <PresenterTopBar
      view={presenterView()}
      state={props.state ?? presentationState()}
      elapsedMs={props.elapsedMs ?? 760000}
      clockMinutes={872}
      onTimer={props.onTimer ?? (() => {})}
      onReopenAudience={props.onReopen ?? (() => {})}
    />
  );
}

test('shows the deck title, the current part, the timer, time left and the clock', async ({
  mount,
  page,
}) => {
  await mount(bar());
  const top = page.getByTestId('presenter-top-bar');
  await expect(top).toContainText('Ship smaller');
  await expect(page.getByTestId('presenter-part')).toHaveText('Slice it');
  await expect(page.getByTestId('presenter-timer')).toHaveText('12:40');
  await expect(page.getByTestId('presenter-time-left')).toContainText('17:20 left');
  await expect(page.getByTestId('presenter-time-left')).toContainText('30:00');
  await expect(page.getByTestId('presenter-clock')).toHaveText('14:32');
});

test('the timer is plain with 6 minutes left', async ({ mount, page }) => {
  await mount(bar({ elapsedMs: 24 * MIN }));
  await expect(page.getByTestId('presenter-timer')).toHaveAttribute('data-tone', 'plain');
  await expect(page.getByTestId('presenter-time-left')).toContainText('6:00 left');
});

test('the timer is amber with 4 minutes left', async ({ mount, page }) => {
  await mount(bar({ elapsedMs: 26 * MIN }));
  await expect(page.getByTestId('presenter-timer')).toHaveAttribute('data-tone', 'amber');
});

test('the timer is red and says over 1 minute past the talk length', async ({ mount, page }) => {
  await mount(bar({ elapsedMs: 31 * MIN }));
  await expect(page.getByTestId('presenter-timer')).toHaveAttribute('data-tone', 'red');
  await expect(page.getByTestId('presenter-time-left')).toContainText('1:00 over');
});

test('without a talk length there is no time left and the timer stays plain', async ({
  mount,
  page,
}) => {
  await mount(bar({ elapsedMs: 100 * MIN, state: presentationState({ talkMinutes: null }) }));
  await expect(page.getByTestId('presenter-time-left')).toHaveCount(0);
  await expect(page.getByTestId('presenter-timer')).toHaveAttribute('data-tone', 'plain');
  await expect(page.getByTestId('presenter-timer')).toHaveText('1:40:00');
});

test('pause and reset call onTimer while running', async ({ mount, page }) => {
  const commands: TimerCommand[] = [];
  await mount(bar({ onTimer: (cmd) => commands.push(cmd) }));
  await page.getByRole('button', { name: 'Pause timer' }).click();
  await page.getByRole('button', { name: 'Reset timer (T)' }).click();
  await expect.poll(() => commands).toEqual(['pause', 'reset']);
});

test('a paused timer offers Start timer, which sends resume', async ({ mount, page }) => {
  const commands: TimerCommand[] = [];
  const state = presentationState({ timer: { state: 'paused', elapsedMs: 760000 } });
  await mount(bar({ state, onTimer: (cmd) => commands.push(cmd) }));
  await page.getByRole('button', { name: 'Start timer' }).click();
  await expect.poll(() => commands).toEqual(['resume']);
});

test('renders a chip per audience window and phone, and none for the presenter window', async ({
  mount,
  page,
}) => {
  const state = presentationState({
    connected: [
      { clientId: 'k-1', role: 'presenter', label: 'Chrome on Windows' },
      { clientId: 'k-2', role: 'audience', label: 'Display 2' },
      { clientId: 'k-3', role: 'audience', label: 'Display 3' },
      { clientId: 'k-4', role: 'phone', label: "Sam's iPhone" },
    ],
  });
  await mount(bar({ state }));
  const chips = page.getByTestId('presenter-chip');
  await expect(chips).toHaveCount(3);
  await expect(chips.nth(0)).toHaveAttribute('data-role', 'audience');
  await expect(chips.nth(2)).toHaveAttribute('data-role', 'phone');
  await expect(chips.nth(2)).toContainText("Sam's iPhone");
  await expect(page.getByTestId('presenter-no-audience')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Reopen audience window' })).toHaveCount(0);
});

test('with no audience window it shows a grey chip and Reopen calls onReopenAudience', async ({
  mount,
  page,
}) => {
  let reopened = 0;
  const state = presentationState({
    connected: [{ clientId: 'k-1', role: 'presenter', label: 'Chrome on Windows' }],
  });
  await mount(bar({ state, onReopen: () => (reopened += 1) }));
  await expect(page.getByTestId('presenter-no-audience')).toBeVisible();
  await page.getByRole('button', { name: 'Reopen audience window' }).click();
  await expect.poll(() => reopened).toBe(1);
});

test('a null or false liveChip renders no wrapper', async ({ mount, page }) => {
  await mount(
    <PresenterTopBar
      view={presenterView()}
      state={presentationState()}
      elapsedMs={0}
      clockMinutes={0}
      onTimer={() => {}}
      onReopenAudience={() => {}}
      liveChip={null}
    />,
  );
  await expect(page.getByTestId('presenter-live-chip')).toHaveCount(0);
});

test('presenter top bar screenshot', async ({ mount, page }) => {
  await mount(<div className="bg-[#0a0e16]">{bar()}</div>);
  await expect(page.getByTestId('presenter-top-bar')).toHaveScreenshot('presenter-top-bar.png');
});

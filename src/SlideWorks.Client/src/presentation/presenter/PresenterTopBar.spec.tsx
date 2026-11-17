import { test, expect } from '@playwright/experimental-ct-react';
import { presentationState, presenterView } from '../../test-support/fixtures/presenting';
import type { TimerCommand } from '../types';
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

test('renders a chip per connection and sends timer commands', async ({ mount, page }) => {
  const commands: TimerCommand[] = [];
  const state = presentationState();
  await mount(
    <PresenterTopBar
      view={presenterView()}
      state={state}
      elapsedMs={760000}
      clockMinutes={872}
      onTimer={(cmd) => commands.push(cmd)}
      onReopenAudience={() => {}}
    />,
  );
  const chips = page.getByTestId('presenter-chip');
  await expect(chips).toHaveCount(state.connected.length);
  await expect(chips.first()).toHaveAttribute('data-role', 'presenter');
  await expect(page.getByTestId('presenter-timer')).toBeVisible();
  await page.getByTestId('presenter-timer-pause').click();
  await page.getByTestId('presenter-timer-reset').click();
  await expect.poll(() => commands).toEqual(['pause', 'reset']);
});

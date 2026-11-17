import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GuestRetryButton, THROTTLED_MESSAGE } from './GuestRetryButton';
import { SLOW_JITTER_MS, THROTTLED_RETRY_MS } from './useAudience';
import type { RetryOutcome } from './useRetry';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
let container: HTMLElement;
const answers: ((outcome: RetryOutcome) => void)[] = [];
// A retry that settles only when the test says so.
const retry = vi.fn(() => new Promise<RetryOutcome>((resolve) => answers.push(resolve)));
const answer = (outcome: RetryOutcome) =>
  act(async () => answers.splice(0).forEach((resolve) => resolve(outcome)));
const button = () => container.querySelector('button')!;
const status = () => container.querySelector('[role="status"]');

beforeEach(async () => {
  vi.useFakeTimers();
  retry.mockClear();
  answers.length = 0;
  container = document.createElement('div');
  root = createRoot(container);
  await act(async () =>
    root.render(
      createElement(GuestRetryButton, { onRetry: retry, testId: 'retry', random: () => 0.5 }),
    ),
  );
});

afterEach(async () => {
  await act(() => root.unmount());
  vi.useRealTimers();
});

describe('GuestRetryButton', () => {
  it('shows a pending state while the retry is in flight and ignores a second press', async () => {
    await act(async () => button().click());
    expect(button().textContent).toBe('Trying…');
    expect(button().getAttribute('aria-busy')).toBe('true');

    await act(async () => button().click());
    expect(retry).toHaveBeenCalledOnce();

    await answer('done');
    expect(button().textContent).toBe('Try again');
    expect(status()).toBeNull();
  });

  it('says so when the venue is rate limited, and retries on its own after the jittered wait', async () => {
    await act(async () => button().click());
    await answer('throttled');
    expect(status()?.textContent).toBe(THROTTLED_MESSAGE);
    expect(button().textContent).toBe('Try again');

    // random 0.5: the wait is the base plus half the jitter.
    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS + SLOW_JITTER_MS / 2 - 1));
    expect(retry).toHaveBeenCalledOnce();
    await act(async () => vi.advanceTimersByTimeAsync(1));
    expect(retry).toHaveBeenCalledTimes(2);

    await answer('done');
    expect(status()).toBeNull();
    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS + SLOW_JITTER_MS));
    expect(retry).toHaveBeenCalledTimes(2);
  });

  it('keeps retrying while the answers stay throttled', async () => {
    await act(async () => button().click());
    await answer('throttled');
    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS + SLOW_JITTER_MS));
    await answer('throttled');
    expect(status()?.textContent).toBe(THROTTLED_MESSAGE);
    await act(async () => vi.advanceTimersByTimeAsync(THROTTLED_RETRY_MS + SLOW_JITTER_MS));
    expect(retry).toHaveBeenCalledTimes(3);
  });
});

import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  GuestOffline,
  OFFLINE_JITTER_MS,
  OFFLINE_MAX_RETRY_MS,
  OFFLINE_RETRY_MS,
} from './GuestOffline';
import { GUEST_TEST_IDS } from './testIds';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
let container: HTMLElement;
const pending: (() => void)[] = [];
// An attempt that settles only when the test says so.
const retry = vi.fn(() => new Promise<void>((resolve) => pending.push(resolve)));
const settle = () => act(async () => pending.splice(0).forEach((resolve) => resolve()));
const wait = (ms: number) => act(async () => vi.advanceTimersByTimeAsync(ms));

beforeEach(async () => {
  vi.useFakeTimers();
  retry.mockClear();
  pending.length = 0;
  container = document.createElement('div');
  root = createRoot(container);
  await act(async () =>
    root.render(createElement(GuestOffline, { onRetry: retry, random: () => 0.5 })),
  );
});

afterEach(async () => {
  await act(() => root.unmount());
  vi.useRealTimers();
});

const firstWait = OFFLINE_RETRY_MS + OFFLINE_JITTER_MS / 2;

describe('GuestOffline retries', () => {
  it('waits the base plus the random extra before the first attempt', async () => {
    await wait(firstWait - 1);
    expect(retry).not.toHaveBeenCalled();
    await wait(1);
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('starts no new attempt while the last one is still running', async () => {
    await wait(firstWait);
    await wait(10 * OFFLINE_MAX_RETRY_MS);
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('doubles the wait after each settled attempt, up to the cap', async () => {
    const waits = [firstWait];
    let delay = OFFLINE_RETRY_MS;
    for (let i = 0; i < 6; i++) {
      delay = Math.min(delay * 2, OFFLINE_MAX_RETRY_MS);
      waits.push(delay + OFFLINE_JITTER_MS / 2);
    }
    expect(waits.slice(1, 4)).toEqual([10000 + 1000, 20000 + 1000, 30000 + 1000]);
    expect(waits.at(-1)).toBe(OFFLINE_MAX_RETRY_MS + OFFLINE_JITTER_MS / 2);

    let calls = 0;
    for (const ms of waits) {
      await wait(ms - 1);
      expect(retry).toHaveBeenCalledTimes(calls);
      await wait(1);
      calls++;
      expect(retry).toHaveBeenCalledTimes(calls);
      await settle();
    }
  });

  it('keeps going after an attempt rejects', async () => {
    retry.mockRejectedValueOnce(new Error('network'));
    await wait(firstWait);
    await wait(10_000 + OFFLINE_JITTER_MS);
    expect(retry).toHaveBeenCalledTimes(2);
  });

  it('Try now retries at once and the next wait is the base again', async () => {
    await wait(firstWait);
    await settle();
    await wait(10_000);
    expect(retry).toHaveBeenCalledTimes(1);

    const button = container.querySelector<HTMLButtonElement>(
      `[data-testid="${GUEST_TEST_IDS.offlineRetry}"]`,
    )!;
    await act(async () => button.click());
    expect(retry).toHaveBeenCalledTimes(2);
    await settle();

    await wait(firstWait - 1);
    expect(retry).toHaveBeenCalledTimes(2);
    await wait(1);
    expect(retry).toHaveBeenCalledTimes(3);
  });

  it('stops when unmounted, including an attempt that settles afterwards', async () => {
    await wait(firstWait);
    await act(() => root.unmount());
    await settle();
    await wait(10 * OFFLINE_MAX_RETRY_MS);
    expect(retry).toHaveBeenCalledTimes(1);
    root = createRoot(document.createElement('div'));
  });
});

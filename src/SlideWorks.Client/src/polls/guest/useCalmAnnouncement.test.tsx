import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useCalmAnnouncement } from './useCalmAnnouncement';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
let host: HTMLDivElement;

function Probe({ text }: { text: string }) {
  return createElement('p', null, useCalmAnnouncement(text, 5000));
}

const show = (text: string) => act(() => root.render(createElement(Probe, { text })));
const wait = (ms: number) => act(() => void vi.advanceTimersByTime(ms));
const said = () => host.textContent;

describe('useCalmAnnouncement', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    host = document.createElement('div');
    root = createRoot(host);
  });
  afterEach(() => {
    act(() => root.unmount());
    vi.useRealTimers();
  });

  it('holds the first text without waiting', () => {
    show('38');
    expect(said()).toBe('38');
  });

  it('announces the first change at once, then at most once per quiet period, and the last text lands', () => {
    show('38');
    show('39');
    wait(0);
    expect(said()).toBe('39');

    show('40');
    show('41');
    wait(4999);
    expect(said()).toBe('39');

    wait(1);
    expect(said()).toBe('41');
  });

  it('announces at once again after a quiet period with no change', () => {
    show('1');
    show('2');
    wait(0);
    wait(5000);

    show('3');
    wait(0);
    expect(said()).toBe('3');
  });

  it('leaves no timer behind when unmounted', () => {
    show('a');
    show('b');
    act(() => root.unmount());
    expect(vi.getTimerCount()).toBe(0);
    root = createRoot(host);
  });
});

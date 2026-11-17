import { act, useRef } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { largestFit, useFitText, type FitBudget } from './useFitText';

// Fits when the size is at most `limit`, recording every size it measured.
function probe(limit: number) {
  const tried: number[] = [];
  const fits = (size: number) => {
    tried.push(size);
    return size <= limit;
  };
  return { tried, fits };
}

describe('largestFit', () => {
  it('keeps the start size when it fits, measuring once', () => {
    const { tried, fits } = probe(200);
    expect(largestFit(168, 96, fits)).toBe(168);
    expect(tried).toEqual([168]);
  });

  it.each([167, 131, 97, 96])('finds the largest size that fits (%i)', (limit) => {
    expect(largestFit(168, 96, probe(limit).fits)).toBe(limit);
  });

  it('stops at the floor when nothing fits', () => {
    expect(largestFit(168, 96, probe(40).fits)).toBe(96);
  });

  it('measures about log2 of the range, not once per pixel', () => {
    const { tried, fits } = probe(121);
    largestFit(300, 120, fits);
    expect(tried.length).toBeLessThanOrEqual(10);
  });
});

// The hook in jsdom, which has no layout: a fake layout gives the fitted
// element a height, and a fake ResizeObserver and document.fonts let each test
// say when the box gets its size and when fonts load. Fonts are loading at
// mount, as when a page first draws.
describe('useFitText', () => {
  // The fitted element's box: 0 × 0 until `laidOut`; then one 100 px wide line
  // while the font size is at most `limit`, and two lines above it.
  const layout = { laidOut: true, limit: 40 };
  const fontSizeOf = (element: HTMLElement) => parseFloat(element.style.fontSize || '60');
  const lineCount = (element: HTMLElement) => (fontSizeOf(element) > layout.limit ? 2 : 1);

  class FakeResizeObserver {
    static live = new Set<FakeResizeObserver>();
    static created = 0;
    readonly targets = new Set<Element>();
    constructor(private readonly callback: ResizeObserverCallback) {
      FakeResizeObserver.created += 1;
      FakeResizeObserver.live.add(this);
    }
    observe(target: Element) {
      this.targets.add(target);
    }
    unobserve(target: Element) {
      this.targets.delete(target);
    }
    disconnect() {
      this.targets.clear();
      FakeResizeObserver.live.delete(this);
    }
    // What the browser does after a layout that changed an observed box.
    static notify() {
      for (const observer of [...FakeResizeObserver.live]) {
        if (observer.targets.size > 0) observer.callback([], observer as never);
      }
    }
  }

  // A fake document.fonts: `status` is 'loading' from startLoadingFonts until
  // flushFonts, and `ready` resolves when they load, as in the browser.
  const fonts = { status: 'loaded' as 'loading' | 'loaded', ready: Promise.resolve() };
  let loadFonts: () => void = () => {};
  const startLoadingFonts = () => {
    fonts.status = 'loading';
    fonts.ready = new Promise<void>((resolve) => (loadFonts = resolve));
  };
  let container: HTMLDivElement;
  let root: Root;

  beforeEach(() => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    layout.laidOut = true;
    layout.limit = 40;
    FakeResizeObserver.live.clear();
    FakeResizeObserver.created = 0;
    vi.stubGlobal('ResizeObserver', FakeResizeObserver);
    startLoadingFonts();
    Object.defineProperty(document, 'fonts', { configurable: true, value: fonts });
    vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockImplementation(function (
      this: HTMLElement,
    ) {
      return layout.laidOut ? 100 : 0;
    });
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockImplementation(function (
      this: HTMLElement,
    ) {
      return layout.laidOut ? lineCount(this) * fontSizeOf(this) : 0;
    });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  // Each measure the fit takes; declared once, so the budget stays the same.
  const measured = vi.fn<(element: HTMLElement) => number>(() => Infinity);
  const BUDGET: FitBudget = { start: 60, floor: 20, lines: 1, maxHeight: measured };

  function Fitted({ text, other = 0 }: { text: string; other?: number }) {
    const ref = useRef<HTMLParagraphElement>(null);
    useFitText(ref, BUDGET, text);
    return (
      <p ref={ref} data-other={other}>
        {text}
      </p>
    );
  }

  const fitted = () => container.querySelector('p')!;
  const flushFonts = async () => {
    fonts.status = 'loaded';
    loadFonts();
    await act(async () => {
      await document.fonts.ready;
    });
  };

  it('fits to the largest size that keeps its lines', () => {
    act(() => root.render(<Fitted text="Ship smaller" />));
    expect(fitted().style.fontSize).toBe('40px');
    expect(FakeResizeObserver.created).toBe(0);
  });

  it('waits with one observer when fonts load while the box is 0', async () => {
    layout.laidOut = false;
    act(() => root.render(<Fitted text="Ship smaller" />));
    await flushFonts();

    // Mount asked for a fit and couldn't measure; the fonts loading meanwhile
    // started no second wait.
    expect(FakeResizeObserver.created).toBe(1);
    expect(measured).not.toHaveBeenCalled();
    expect(fitted().style.fontSize).toBe('');
  });

  it('fits a box that gets its size 2 s later, then stops observing', async () => {
    vi.useFakeTimers();
    layout.laidOut = false;
    act(() => root.render(<Fitted text="Ship smaller" />));
    await flushFonts();
    // Long past the ~1 s (60 frames) the fit used to wait before giving up.
    act(() => vi.advanceTimersByTime(2000));
    expect(fitted().style.fontSize).toBe('');

    layout.laidOut = true;
    act(() => FakeResizeObserver.notify());

    expect(fitted().style.fontSize).toBe('40px');
    expect(FakeResizeObserver.live.size).toBe(0);
  });

  it('fits again once the fonts loading at its fit arrive', async () => {
    act(() => root.render(<Fitted text="Ship smaller" />));
    expect(fitted().style.fontSize).toBe('40px');

    // The web font is wider than the fallback the first fit measured.
    layout.limit = 30;
    await flushFonts();

    expect(fitted().style.fontSize).toBe('30px');
  });

  it('a box shown after fonts loaded fits again once the face it now needs arrives', async () => {
    // Hidden: nothing draws its face, so fonts.ready resolves with it unloaded.
    layout.laidOut = false;
    act(() => root.render(<Fitted text="Ship smaller" />));
    await flushFonts();

    // Shown: the fit's measure starts loading the face, and measures the
    // fallback meanwhile.
    layout.laidOut = true;
    startLoadingFonts();
    act(() => FakeResizeObserver.notify());
    expect(fitted().style.fontSize).toBe('40px');

    layout.limit = 30;
    await flushFonts();
    expect(fitted().style.fontSize).toBe('30px');
    expect(FakeResizeObserver.live.size).toBe(0);
  });

  it('stops waiting when it unmounts', () => {
    layout.laidOut = false;
    act(() => root.render(<Fitted text="Ship smaller" />));
    expect(FakeResizeObserver.live.size).toBe(1);
    act(() => root.render(<p />));
    expect(FakeResizeObserver.live.size).toBe(0);
  });

  it('fits once per text change, never on other renders', async () => {
    measured.mockClear();
    act(() => root.render(<Fitted text="Ship smaller" />));
    await flushFonts();
    const afterMount = measured.mock.calls.length;
    expect(afterMount).toBeGreaterThan(0);

    for (let other = 1; other <= 3; other++)
      act(() => root.render(<Fitted text="Ship smaller" other={other} />));
    expect(measured.mock.calls.length).toBe(afterMount);

    layout.limit = 30;
    act(() => root.render(<Fitted text="Ship smaller, sooner" other={3} />));
    expect(measured.mock.calls.length).toBeGreaterThan(afterMount);
    expect(fitted().style.fontSize).toBe('30px');
  });
});

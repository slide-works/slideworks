import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GuestLanguageContext } from '../guest/language/GuestLanguageContext';
import { useOpenResponse } from './useOpenResponse';

const api = vi.hoisted(() => ({ get: vi.fn(), put: vi.fn() }));
vi.mock('../api/client', async (original) => ({
  ...(await original<typeof import('../api/client')>()),
  api,
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;

function Probe() {
  useOpenResponse('482913', 3, 'live');
  return null;
}

beforeEach(() => {
  api.get.mockReset();
  api.get.mockReturnValue(new Promise(() => undefined));
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
});

describe('useOpenResponse language', () => {
  it('reads the response view in the phone language from the context', async () => {
    await act(async () =>
      root.render(createElement(GuestLanguageContext, { value: 'th' }, createElement(Probe))),
    );

    expect(api.get).toHaveBeenCalledExactlyOnceWith(
      '/api/join/482913/slides/3/response?language=th',
    );
  });

  it('reads the response view without a query when the phone has no language', async () => {
    await act(async () => root.render(createElement(Probe)));

    expect(api.get).toHaveBeenCalledExactlyOnceWith('/api/join/482913/slides/3/response');
  });
});

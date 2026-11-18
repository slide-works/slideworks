import { afterEach, describe, expect, it, vi } from 'vitest';
import type { LookFont } from '../api/look';
import { fontsSettled } from './fontsSettled';

// jsdom has neither FontFace nor document.fonts: stand-ins hold what the module asks of them.
type Face = { family: string; load: () => Promise<Face> };
const added: Face[] = [];

function stubFonts(load: (family: string) => Promise<unknown>) {
  class FakeFace implements Face {
    constructor(readonly family: string) {}
    load = () => load(this.family).then(() => this);
  }
  vi.stubGlobal('FontFace', FakeFace);
  Object.defineProperty(document, 'fonts', {
    configurable: true,
    value: {
      add: (face: Face) => added.push(face),
      delete: (face: Face) => added.splice(added.indexOf(face), 1),
      load: () => Promise.resolve([]),
      ready: Promise.resolve(),
    },
  });
}

const font = (family: string): LookFont => ({
  family,
  role: 'head',
  url: `/api/templates/tpl_x/assets/${family}`,
  weight: 400,
  style: 'normal',
});

afterEach(() => {
  vi.unstubAllGlobals();
  added.length = 0;
});

describe('fontsSettled', () => {
  it('settles when a face fails', async () => {
    stubFonts(() => Promise.reject(new Error('404')));

    await expect(fontsSettled({ fonts: [font('Broken')] })).resolves.toBeUndefined();
    expect(added).toEqual([]);
  });

  it('tries a failed face again on the next call', async () => {
    const loads: string[] = [];
    stubFonts((family) => {
      loads.push(family);
      return Promise.reject(new Error('404'));
    });
    const look = { fonts: [font('Retry')] };

    await expect(fontsSettled(look)).resolves.toBeUndefined();
    await expect(fontsSettled(look)).resolves.toBeUndefined();
    expect(loads).toEqual(['Retry', 'Retry']);
    expect(added).toEqual([]);
  });

  it('settles at once for a built-in look', async () => {
    stubFonts(() => new Promise(() => undefined));

    await expect(fontsSettled({ fonts: [] })).resolves.toBeUndefined();
    await expect(fontsSettled(undefined)).resolves.toBeUndefined();
    expect(added).toEqual([]);
  });

  it('waits for a face that is still loading', async () => {
    let finish: () => void = () => undefined;
    stubFonts(() => new Promise<void>((resolve) => (finish = resolve)));
    let settled = false;

    const done = fontsSettled({ fonts: [font('Slow')] }).then(() => (settled = true));
    await Promise.resolve();
    expect(settled).toBe(false);
    finish();
    await done;
    expect(settled).toBe(true);
  });
});

import { afterEach, describe, expect, it, vi } from 'vitest';
import { confirmFontRights, uploadAsset } from './uploadAsset';

const stored = {
  assetId: 'a1',
  kind: 'font',
  fileName: 'a.woff2',
  contentType: 'font/woff2',
  bytes: 3,
  meta: { avgCharEm: 0.56 },
};
const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const file = new File([new Uint8Array([1, 2, 3])], 'a.woff2', { type: 'font/woff2' });

afterEach(() => vi.unstubAllGlobals());

function stub(answers: (Response | Error)[]) {
  const calls: { url: string; init: RequestInit }[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init: RequestInit) => {
      calls.push({ url, init });
      const next = answers.shift();
      return next instanceof Error ? Promise.reject(next) : Promise.resolve(next);
    }),
  );
  return calls;
}

describe('uploadAsset', () => {
  it('posts the start, then puts the bytes to the address on its own origin with no cookie', async () => {
    const calls = stub([
      json(201, {
        uploadUrl: 'https://elsewhere.example/api/template-uploads/tok?x=1',
        expiresLabel: 'in 10 minutes',
      }),
      json(200, stored),
    ]);
    const outcome = await uploadAsset('tpl_1', 'font', file, true);
    expect(outcome).toEqual({ ok: true, asset: stored });
    expect(calls[0].url).toBe('/api/templates/tpl_1/assets');
    expect(calls[0].init.method).toBe('POST');
    expect(JSON.parse(calls[0].init.body as string)).toEqual({
      kind: 'font',
      fileName: 'a.woff2',
      rightsConfirmed: true,
    });
    expect(calls[1].url).toBe('/api/template-uploads/tok?x=1');
    expect(calls[1].init.method).toBe('PUT');
    expect(calls[1].init.credentials).toBe('omit');
    expect(calls[1].init.body).toBe(file);
  });

  it('starts again from the first call after a refusal', async () => {
    const calls = stub([
      json(201, { uploadUrl: '/api/template-uploads/one', expiresLabel: 'x' }),
      json(400, {
        error: 'font_not_woff2',
        message: 'Fonts must be woff2 files. This one is TrueType.',
      }),
      json(201, { uploadUrl: '/api/template-uploads/two', expiresLabel: 'x' }),
      json(200, stored),
    ]);
    const first = await uploadAsset('tpl_1', 'font', file, true);
    expect(first).toEqual({
      ok: false,
      kind: 'refused',
      code: 'font_not_woff2',
      message: 'Fonts must be woff2 files. This one is TrueType.',
    });
    expect(calls).toHaveLength(2);
    const second = await uploadAsset('tpl_1', 'font', file, true);
    expect(second.ok).toBe(true);
    expect(calls.map((call) => call.url)).toEqual([
      '/api/templates/tpl_1/assets',
      '/api/template-uploads/one',
      '/api/templates/tpl_1/assets',
      '/api/template-uploads/two',
    ]);
  });

  it('says unreachable when the network fails, at either call', async () => {
    stub([new TypeError('Failed to fetch')]);
    expect(await uploadAsset('tpl_1', 'logo', file, false)).toMatchObject({
      ok: false,
      kind: 'unreachable',
    });
    stub([
      json(201, { uploadUrl: '/api/template-uploads/t', expiresLabel: 'x' }),
      new TypeError('x'),
    ]);
    expect(await uploadAsset('tpl_1', 'logo', file, false)).toMatchObject({
      ok: false,
      kind: 'unreachable',
    });
  });

  it('reports a failure that is not a refusal as failed, with a sentence', async () => {
    stub([
      json(201, { uploadUrl: '/api/template-uploads/t', expiresLabel: 'x' }),
      json(404, { error: 'Upload not found' }),
    ]);
    expect(await uploadAsset('tpl_1', 'art', file, false)).toEqual({
      ok: false,
      kind: 'failed',
      message: 'Upload not found',
    });
  });

  it('never puts the file anywhere but under /api/template-uploads/', async () => {
    const calls = stub([json(201, { uploadUrl: 'https://evil.example/steal', expiresLabel: 'x' })]);
    expect(await uploadAsset('tpl_1', 'art', file, false)).toMatchObject({ kind: 'failed' });
    expect(calls).toHaveLength(1);
  });
});

describe('confirmFontRights', () => {
  it('posts to the font-rights route and answers the count', async () => {
    const calls = stub([json(200, { confirmed: 4 })]);
    expect(await confirmFontRights('tpl_1')).toBe(4);
    expect(calls[0].url).toBe('/api/templates/tpl_1/assets/font-rights');
    expect(calls[0].init.method).toBe('POST');
  });
});

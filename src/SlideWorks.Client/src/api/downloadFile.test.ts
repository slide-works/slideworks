import { afterEach, describe, expect, it, vi } from 'vitest';
import { DOWNLOAD_FALLBACK_ERROR, downloadFile } from './downloadFile';

afterEach(() => vi.unstubAllGlobals());

describe('downloadFile', () => {
  it('downloadFile_WhenOk_ShouldClickALinkNamedAfterTheFileAndRevokeTheUrl', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('%PDF', { status: 200 })),
    );
    const created = vi.fn(() => 'blob:x');
    const revoked = vi.fn();
    vi.stubGlobal('URL', { createObjectURL: created, revokeObjectURL: revoked });
    const names: string[] = [];
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      names.push(this.download);
    });

    await downloadFile('/api/decks/d1/pdf', 'my-deck.pdf');

    expect(names).toEqual(['my-deck.pdf']);
    expect(revoked).toHaveBeenCalledWith('blob:x');
    click.mockRestore();
  });

  it('downloadFile_WhenTheServerRefuses_ShouldThrowItsErrorText', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ error: 'Busy.' }), { status: 503 })),
    );
    await expect(downloadFile('/api/decks/d1/pdf', 'a.pdf')).rejects.toThrow('Busy.');
  });

  it('downloadFile_WhenTheBodyHasNoError_ShouldThrowTheFallback', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('nope', { status: 502 })),
    );
    await expect(downloadFile('/api/decks/d1/pdf', 'a.pdf')).rejects.toThrow(
      DOWNLOAD_FALLBACK_ERROR,
    );
  });

  it('downloadFile_WhenTheNetworkFails_ShouldThrowTheFallback', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('offline');
      }),
    );
    await expect(downloadFile('/api/decks/d1/pdf', 'a.pdf')).rejects.toThrow(
      DOWNLOAD_FALLBACK_ERROR,
    );
  });
});

import { afterEach, describe, expect, it, vi } from 'vitest';
import { GUEST_HEADER, getJoin, isGuestId } from './guest';
import { configureTestPlatform, stubFetch } from './test-support/testPlatform';

afterEach(() => vi.unstubAllGlobals());

const view = {
  deckTitle: 'Ship smaller',
  presenterName: 'Sam Rivera',
  state: { type: 'audienceState', seq: 1, phase: 'live', current: { index: 2, count: 16 } },
};
const issued = '0123456789abcdef0123456789abcdef';

describe('getJoin', () => {
  it('returns the issued guest id from the response header', async () => {
    configureTestPlatform();
    const fetchMock = stubFetch(200, view, { [GUEST_HEADER]: issued });

    const result = await getJoin('https://talk.example', '123 456');

    expect(result).toEqual({ view, issuedGuestId: issued });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://talk.example/api/join/123%20456');
    expect(init.headers).toEqual({
      'X-SlideWorks-Client': 'app',
      'X-SlideWorks-Guest': 'id-for-https://talk.example',
    });
  });

  it('returns no guest id when the server issued none', async () => {
    configureTestPlatform();
    stubFetch(200, view);

    expect((await getJoin('https://talk.example', '123456')).issuedGuestId).toBeNull();
  });

  it('throws the refusal and calls no handler on a refused code', async () => {
    const { onUnauthorized } = configureTestPlatform();
    stubFetch(404, { error: 'no_session' });

    await expect(getJoin('https://talk.example', '000000')).rejects.toThrow('no_session');
    expect(onUnauthorized).not.toHaveBeenCalled();
  });
});

describe('isGuestId', () => {
  it('accepts 32 lowercase hex digits only', () => {
    expect(isGuestId(issued)).toBe(true);
    expect(isGuestId(issued.toUpperCase())).toBe(false);
    expect(isGuestId(issued.slice(1))).toBe(false);
    expect(isGuestId(`${issued}0`)).toBe(false);
  });
});

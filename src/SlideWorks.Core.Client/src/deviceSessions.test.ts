import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  createDeviceSessionCode,
  endCurrentDeviceSession,
  exchangeDeviceSessionCode,
  listDeviceSessions,
  revokeDeviceSession,
} from './deviceSessions';
import { configureTestPlatform, stubFetch } from './test-support/testPlatform';

afterEach(() => vi.unstubAllGlobals());

describe('deviceSessions', () => {
  it('each function calls its method and path', async () => {
    configureTestPlatform({ apiOrigin: () => 'https://server.example' });
    const fetchMock = stubFetch(200, {});
    const code = { challenge: 'c'.repeat(43), state: 's'.repeat(16), deviceName: 'Pixel 9' };
    const exchange = { code: 'abc', verifier: 'v'.repeat(43) };

    await createDeviceSessionCode(code);
    await exchangeDeviceSessionCode(exchange);
    await listDeviceSessions();
    await revokeDeviceSession('6b1f3c2e-8a4d-4e5f-9c7b-2d1e0f3a4b5c');
    await endCurrentDeviceSession();

    const calls = fetchMock.mock.calls.map(([url, init]) => [init.method, url, init.body]);
    expect(calls).toEqual([
      ['POST', 'https://server.example/api/auth/device-sessions/codes', JSON.stringify(code)],
      [
        'POST',
        'https://server.example/api/auth/device-sessions/exchange',
        JSON.stringify(exchange),
      ],
      ['GET', 'https://server.example/api/auth/device-sessions', undefined],
      [
        'DELETE',
        'https://server.example/api/auth/device-sessions/6b1f3c2e-8a4d-4e5f-9c7b-2d1e0f3a4b5c',
        undefined,
      ],
      ['DELETE', 'https://server.example/api/auth/device-sessions/current', undefined],
    ]);
  });
});

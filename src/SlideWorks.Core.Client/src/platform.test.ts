import { describe, expect, it } from 'vitest';
import { platform } from './platform';

// Nothing in this file configures a platform, so it sees the defaults (Vitest isolates each test file's modules).
describe('platform', () => {
  it('defaults before configure', () => {
    const host = platform();

    host.persistentStore().setItem('k', 'persistent');
    host.sessionStore().setItem('k', 'session');
    const unsubscribe = host.onOnline(() => undefined);

    expect(host.persistentStore().getItem('k')).toBe('persistent');
    expect(host.sessionStore().getItem('k')).toBe('session');
    host.persistentStore().removeItem('k');
    expect(host.persistentStore().getItem('k')).toBeNull();
    expect(host.isOffline()).toBe(false);
    expect(unsubscribe).toBeTypeOf('function');
    expect(host.apiOrigin()).toBe('');
    expect(host.authHeaders()).toEqual({});
    expect(host.guestHeaders('https://other.example')).toEqual({});
    expect(host.credentials()).toBe('same-origin');
    expect(() => host.onUnauthorized('session_ended')).not.toThrow();
    expect(() => host.onPendingApproval()).not.toThrow();
  });
});

import type { KeyValueStore } from '@slideworks/core/platform';
import { describe, expect, it } from 'vitest';
import { createGuestStore, GUEST_IDLE_LIMIT_MS, guestKey } from './guestStore';

const hosted = 'https://slideworks.app';
const selfHosted = 'https://slides.example.org';
const idA = '0123456789abcdef0123456789abcdef';
const idB = 'fedcba9876543210fedcba9876543210';

function setup() {
  const values = new Map<string, string>();
  const kv: KeyValueStore = {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => void values.set(key, value),
    removeItem: (key) => void values.delete(key),
  };
  const clock = { now: 1_000_000 };
  return { values, store: createGuestStore({ kv, now: () => clock.now }), clock };
}

describe('guestStore', () => {
  it('keeps an id per origin', () => {
    const { store, values } = setup();
    store.remember(hosted, idA);
    store.remember(selfHosted, idB);

    expect(store.use(hosted)).toBe(idA);
    expect(store.use(selfHosted)).toBe(idB);
    expect(store.use('https://other.example')).toBeNull();
    expect(JSON.parse(values.get(guestKey(hosted)) ?? '')).toEqual({
      id: idA,
      lastUsedAt: 1_000_000,
    });
  });

  it('drops an id unused for 12 hours', () => {
    const { store, values, clock } = setup();
    store.remember(hosted, idA);
    clock.now += GUEST_IDLE_LIMIT_MS - 1;
    expect(store.use(hosted)).toBe(idA);

    clock.now += GUEST_IDLE_LIMIT_MS;
    expect(store.use(hosted)).toBeNull();
    expect(values.has(guestKey(hosted))).toBe(false);
  });

  it('touches on use', () => {
    const { store, values, clock } = setup();
    store.remember(hosted, idA);
    clock.now += 60_000;
    store.use(hosted);

    expect(JSON.parse(values.get(guestKey(hosted)) ?? '').lastUsedAt).toBe(1_060_000);
  });
});

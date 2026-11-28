import type { KeyValueStore } from '@slideworks/core/platform';

// The app's guest ids (contract K7, decision D7): one per server origin, kept in the kv store under
// slideworks.guest.<origin> as { id, lastUsedAt }, and dropped after 12 hours unused, like the web's sw_guest cookie.
// Signing out never touches them.

export const GUEST_KEY_PREFIX = 'slideworks.guest.';
export const GUEST_IDLE_LIMIT_MS = 12 * 60 * 60 * 1000;

export type StoredGuest = { id: string; lastUsedAt: number };

export interface GuestStore {
  // The id held for that origin, or null; using it counts as use, so it moves lastUsedAt to now.
  use(origin: string): string | null;
  // Keeps the id a join issued for that origin.
  remember(origin: string, id: string): void;
  forget(origin: string): void;
}

export function guestKey(origin: string): string {
  return GUEST_KEY_PREFIX + origin;
}

function parse(raw: string | null): StoredGuest | null {
  if (!raw) return null;
  try {
    const value: unknown = JSON.parse(raw);
    if (typeof value !== 'object' || value === null) return null;
    const { id, lastUsedAt } = value as Record<string, unknown>;
    if (typeof id !== 'string' || typeof lastUsedAt !== 'number') return null;
    return { id, lastUsedAt };
  } catch {
    return null;
  }
}

export function createGuestStore(deps: { kv: KeyValueStore; now: () => number }): GuestStore {
  const { kv, now } = deps;
  const write = (origin: string, id: string) =>
    kv.setItem(guestKey(origin), JSON.stringify({ id, lastUsedAt: now() } satisfies StoredGuest));

  return {
    use(origin) {
      const stored = parse(kv.getItem(guestKey(origin)));
      if (!stored) return null;
      if (now() - stored.lastUsedAt >= GUEST_IDLE_LIMIT_MS) {
        kv.removeItem(guestKey(origin));
        return null;
      }
      write(origin, stored.id);
      return stored.id;
    },
    remember: write,
    forget: (origin) => kv.removeItem(guestKey(origin)),
  };
}

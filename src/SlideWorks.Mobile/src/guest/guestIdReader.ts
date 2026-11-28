import type { GuestStore } from './guestStore';

// What the platform's guestHeaders(origin) and a guest hub connection's guestId() read: the id held for that origin,
// or null. Reading it is a use of the guest identity, so it keeps the id alive.
export type GuestIdReader = (origin: string) => string | null;

export function guestIdReader(store: GuestStore): GuestIdReader {
  return (origin) => store.use(origin);
}

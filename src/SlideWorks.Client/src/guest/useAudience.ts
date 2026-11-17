import type { AudienceState } from './audienceEvents';

export type AudienceStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';

export type Audience = { state: AudienceState; status: AudienceStatus };

// The guest's realtime state (SW-5 J5). initial is the state GET /api/join/{code} returned; the hook lands on the
// current state after a reconnect, never replaying.
export function useAudience(code: string, initial: AudienceState): Audience {
  // TODO: replace with the real hook: connect through createAudienceConnection, JoinAudience(code) on every connection
  // up, apply each audienceState with a higher seq, and report connecting, reconnecting and offline.
  void code;
  return { state: initial, status: 'live' };
}

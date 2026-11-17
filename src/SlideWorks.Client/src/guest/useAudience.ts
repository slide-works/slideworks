import type { AudienceState } from './audienceEvents';
import type { GuestView } from './guestView';

export type AudienceStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';

export type Audience = { view: GuestView; state: AudienceState; status: AudienceStatus };

// The guest's realtime state (SW-5 J5). initial is the view GET /api/join/{code} returned; the hook returns the view the
// screens show, which it re-reads when the state's revision changes, and lands on the current state after a reconnect,
// never replaying.
export function useAudience(code: string, initial: GuestView): Audience {
  // TODO: replace with the real hook: connect through createAudienceConnection, JoinAudience(code) on every connection
  // up, apply each audienceState with a higher seq, re-read GET /api/join/{code} when revision changes, and report
  // connecting, reconnecting and offline.
  void code;
  return { view: initial, state: initial.state, status: 'live' };
}

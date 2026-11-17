// SW-5's guest contract samples (J3, J4) as typed fixtures, built from the JSON in src/contracts. Each call returns a
// fresh copy, so a test may change its own. guestStates.test.ts checks the exact key sets and the literal values.
import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestJson from '@contracts/screens/guest.json' with { type: 'json' };
import type { AudienceEnded, AudienceState, JoinedCount } from '../../guest/audienceEvents';
import type { GuestView } from '../../guest/guestView';
import type { Widen } from './widen';

// tsc can't tie a JSON slide's slots to its layout (see JsonView in widen.ts), so the slots are opaque here and the test
// checks them against the sample deck's slide at run time.
type JsonCurrent = Widen<Omit<NonNullable<AudienceState['current']>, 'slots'>> & { slots: object };
type JsonState = Widen<Omit<AudienceState, 'current'>> & { current: JsonCurrent | null };
type JsonGuest = Widen<Omit<GuestView, 'state'>> & { state: JsonState };

export type GuestPhaseName = keyof typeof guestJson;

const GUESTS = guestJson satisfies Record<GuestPhaseName, JsonGuest>;
const EVENTS = audienceEventsJson satisfies {
  audienceState: JsonState;
  audienceEnded: Widen<AudienceEnded>;
  joinedCount: Widen<JoinedCount>;
};

// GET /api/join/{code} in each phase: live (on slide 3 of the sample deck), lobby and ended.
export function guestView(phase: GuestPhaseName = 'live'): GuestView {
  return structuredClone(GUESTS[phase]) as unknown as GuestView;
}

// One audienceEvent of each type, as it arrives on the hub.
export function audienceState(overrides: Partial<AudienceState> = {}): AudienceState {
  return { ...(structuredClone(EVENTS.audienceState) as unknown as AudienceState), ...overrides };
}

export function audienceEnded(): AudienceEnded {
  return structuredClone(EVENTS.audienceEnded) as AudienceEnded;
}

export function joinedCount(count?: number): JoinedCount {
  const event = structuredClone(EVENTS.joinedCount) as JoinedCount;
  return count === undefined ? event : { ...event, count };
}

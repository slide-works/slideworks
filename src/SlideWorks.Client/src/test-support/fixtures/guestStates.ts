// SW-5's guest contract samples (J3, J4) as typed fixtures, built from the JSON in src/contracts. Each call returns a
// fresh copy, so a test may change its own. guestStates.test.ts checks the exact key sets and the literal values.
import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestJson from '@contracts/screens/guest.json' with { type: 'json' };
import type { AudienceEnded, AudienceState, JoinedCount } from '../../guest/audienceEvents';
import type { GuestTranslations, GuestView } from '../../guest/guestView';
import type { Widen } from './widen';

// tsc can't tie a JSON slide's slots to its layout (see JsonView in widen.ts), so the slots are opaque here and the test
// checks them against the layout's slot keys at run time.
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

// GET /api/join/{code} in each phase: live (on slide 3 of the sample deck), lobby and ended. The live sample's state
// carries room (questions open) and is in Thai with 2 fallback lines, and the live view offers en, es, th and ja
// (SW-883 K5); lobby and ended have translations off. Pass { room: null } for a live page with no Q&A, and
// { language: null } with translationsOff() for a live page with translations off.
export function guestView(
  phase: GuestPhaseName = 'live',
  overrides: Partial<Pick<AudienceState, 'room' | 'language'>> = {},
): GuestView {
  const view = structuredClone(GUESTS[phase]) as unknown as GuestView;
  return { ...view, state: { ...view.state, ...overrides } };
}

// The live view with translations off (SW-883 K5): no translations and a state with no language, as the server sends
// for a deck that never turned them on.
export function translationsOff(view: GuestView = guestView('live')): GuestView {
  return { ...view, translations: null, state: { ...view.state, language: null } };
}

// The live sample's translations: en (the primary, 100), es (100), th (83) and ja (40).
export function guestTranslations(): GuestTranslations {
  return structuredClone(GUESTS.live.translations) as GuestTranslations;
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

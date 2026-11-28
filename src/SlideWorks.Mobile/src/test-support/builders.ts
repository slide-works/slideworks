// Builders for the DTOs the app reads, made from the JSON samples the server's contract tests pin
// (src/contracts/screens/*.json and src/contracts/presentation-state.json), so the app and the server can't drift.
// Each sample `satisfies Widen<CoreType>`; builders.test.ts checks the literal values tsc can't.
import type { DecksLibrarySummary, DeckRowSummary } from '@slideworks/core/decks';
import type { DeviceRow, DeviceSessionExchangeView } from '@slideworks/core/deviceSessions';
import type { JoinSummary } from '@slideworks/core/guest';
import type { MeView } from '@slideworks/core/me';
import type {
  AudienceStateSummary,
  PresentationJoinedSummary,
} from '@slideworks/core/realtime/shapes';
import audienceEventsJson from '../../../contracts/screens/audience-events.json';
import decksLibraryJson from '../../../contracts/screens/decks-library.json';
import deviceSessionExchangeJson from '../../../contracts/screens/device-session-exchange.json';
import deviceSessionsJson from '../../../contracts/screens/device-sessions.json';
import guestJson from '../../../contracts/screens/guest.json';
import meJson from '../../../contracts/screens/me.json';
import presentationStateJson from '../../../contracts/presentation-state.json';
import type { Widen } from './widen';

export const samples = {
  me: meJson satisfies Widen<MeView> as MeView,
  decksLibrary: decksLibraryJson satisfies Widen<DecksLibrarySummary> as DecksLibrarySummary,
  deviceSessions: deviceSessionsJson satisfies Widen<{ devices: DeviceRow[] }> as {
    devices: DeviceRow[];
  },
  deviceSessionExchange:
    deviceSessionExchangeJson satisfies Widen<DeviceSessionExchangeView> as DeviceSessionExchangeView,
  join: guestJson.live satisfies Widen<JoinSummary> as JoinSummary,
  audienceState:
    audienceEventsJson.audienceState satisfies Widen<AudienceStateSummary> as AudienceStateSummary,
  presentationState: presentationStateJson satisfies Widen<PresentationJoinedSummary['state']>,
};

const copy = <T>(value: T): T => JSON.parse(JSON.stringify(value));

export const aMe = (overrides: Partial<MeView> = {}): MeView => ({
  ...copy(samples.me),
  ...overrides,
});

export const aDeckRow = (overrides: Partial<DeckRowSummary> = {}): DeckRowSummary => ({
  ...copy(samples.decksLibrary.decks[0]),
  ...overrides,
});

export const aDecksLibrary = (
  overrides: Partial<DecksLibrarySummary> = {},
): DecksLibrarySummary => ({
  ...copy(samples.decksLibrary),
  ...overrides,
});

export const aDeviceRow = (overrides: Partial<DeviceRow> = {}): DeviceRow => ({
  ...copy(samples.deviceSessions.devices[0]),
  ...overrides,
});

export const aDeviceSessionExchange = (
  overrides: Partial<DeviceSessionExchangeView> = {},
): DeviceSessionExchangeView => ({ ...copy(samples.deviceSessionExchange), ...overrides });

export const aJoinSummary = (overrides: Partial<JoinSummary> = {}): JoinSummary => ({
  ...copy(samples.join),
  ...overrides,
});

export const anAudienceState = (
  overrides: Partial<AudienceStateSummary> = {},
): AudienceStateSummary => ({ ...copy(samples.audienceState), ...overrides });

// JoinPresentation's answer: the slide and step of src/contracts/presentation-state.json.
export const aPresentationJoined = (
  state: Partial<PresentationJoinedSummary['state']> = {},
): PresentationJoinedSummary => ({
  state: { slide: samples.presentationState.slide, step: samples.presentationState.step, ...state },
});

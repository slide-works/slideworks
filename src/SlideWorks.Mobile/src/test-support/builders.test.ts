import { describe, expect, it } from 'vitest';
import {
  aDeckRow,
  aDeviceRow,
  aDeviceSessionExchange,
  aJoinSummary,
  aMe,
  anAudienceState,
  aPresentationJoined,
} from './builders';

// tsc checks each sample's shape (`satisfies Widen<…>` in builders.ts); this checks the literal values and the fields
// the app reads, which a widened type can't.
describe('builders', () => {
  it('each sample satisfies its core type', () => {
    expect(['pending', 'approved']).toContain(aMe().access);
    expect(Object.keys(aDeckRow())).toEqual(
      expect.arrayContaining(['id', 'title', 'updatedLabel']),
    );
    expect(aDeviceRow()).toEqual({
      id: expect.any(String),
      deviceName: expect.any(String),
      signedInLabel: expect.any(String),
      lastUsedLabel: expect.any(String),
      current: expect.any(Boolean),
    });
    expect(aDeviceSessionExchange().secret).toMatch(/^swd_/);
    expect(aDeviceSessionExchange().me.access).toMatch(/^(pending|approved)$/);
    const join = aJoinSummary();
    expect([typeof join.deckTitle, typeof join.presenterName]).toEqual(['string', 'string']);
    for (const state of [join.state, anAudienceState()]) {
      expect(state.type).toBe('audienceState');
      expect(['lobby', 'live', 'ended']).toContain(state.phase);
      expect(typeof state.seq).toBe('number');
    }
    expect(aPresentationJoined()).toEqual({
      state: { slide: expect.any(Number), step: expect.any(Number) },
    });
  });
});

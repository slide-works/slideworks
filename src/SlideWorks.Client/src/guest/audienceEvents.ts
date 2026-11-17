import type { PollActivity, PollControl, PollTally } from '../polls/types';
import type { LayoutName, Slots } from '../slides/slots';

// Mirrors src/contracts/screens/audience-events.json and AudienceEvent.cs (SW-5 J4). Every event goes on the hub method
// "audienceEvent" and has a "type" discriminator.
export const AUDIENCE_EVENT_METHOD = 'audienceEvent';

export type AudiencePhase = 'lobby' | 'live' | 'ended';

// The one slide on screen, projected with no notes and no deep-dive mark (D15). One variant per layout, so narrowing on
// `layout` narrows `slots`.
export type AudienceCurrent = {
  [L in LayoutName]: {
    index: number;
    count: number;
    layout: L;
    slots: Slots[L];
    steps: number;
    step: number;
    black: boolean;
  };
}[LayoutName];

// The guest's view of the talk: a position, not content. seq only increases; current is null unless phase is "live";
// activity is the poll on screen (SW-6 V5), and null on a slide with no poll and in every phase but "live".
export type AudienceState = {
  type: 'audienceState';
  seq: number;
  phase: AudiencePhase;
  current: AudienceCurrent | null;
  revision: number;
  activity: PollActivity | null;
};

// The session ended: the page shows the thank-you state.
export type AudienceEnded = { type: 'audienceEnded' };

// Owner only (audience-watch group): how many guests are joined.
export type JoinedCount = { type: 'joinedCount'; count: number };

// SW-6 (V5): a round's tally, to guests and the big screen (counts null while hidden from the receiver), and the
// presenter's poll view, to the presenter's screens only. Their shapes are in polls/types.ts.
export type AudienceEvent = AudienceState | AudienceEnded | JoinedCount | PollTally | PollControl;

export function isAudienceEvent(value: unknown): value is AudienceEvent {
  if (typeof value !== 'object' || value === null) return false;
  const type = (value as { type?: unknown }).type;
  return (
    type === 'audienceState' ||
    type === 'audienceEnded' ||
    type === 'joinedCount' ||
    type === 'pollTally' ||
    type === 'pollControl'
  );
}

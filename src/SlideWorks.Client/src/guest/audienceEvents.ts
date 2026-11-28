import type { OpenTextEvent, WordCloudEvent } from '../openResponses/types';
import type { PollActivity, PollControl, PollTally } from '../polls/types';
import type { QaRoom, QuestionControl, QuestionTally, ReactionsBurst } from '../qa/types';
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
    surface: 'light' | 'dark';
  };
}[LayoutName];

// The language a guest's state is composed in (SW-883 K5). fallbackLines is the slide on screen's missing plus stale
// lines, which the phone shows in the primary language; untranslated emphasis entries are not counted (D24).
export type AudienceLanguage = { code: string; fallbackLines: number };

// The guest's view of the talk: a position, not content. seq only increases; current is null unless phase is "live";
// activity is the poll on screen (SW-6 V5), and null on a slide with no poll and in every phase but "live". room is
// what the room may do with questions and reactions on the slide on screen (SW-9 Q1), null in every phase but "live".
// language is the language the state is composed in (SW-883 K5), null while the deck's translations are off.
export type AudienceState = {
  type: 'audienceState';
  seq: number;
  phase: AudiencePhase;
  current: AudienceCurrent | null;
  revision: number;
  activity: PollActivity | null;
  room: QaRoom | null;
  language: AudienceLanguage | null;
};

// The session ended: the page shows the thank-you state.
export type AudienceEnded = { type: 'audienceEnded' };

// Owner only (audience-watch group): how many guests are joined.
export type JoinedCount = { type: 'joinedCount'; count: number };

// SW-6 (V5): a round's tally, to guests and the big screen (counts null while hidden from the receiver), and the
// presenter's poll view, to the presenter's screens only. Their shapes are in polls/types.ts.
// SW-9 (Q5): the questions' votes, to guests and the big screen; the presenter's Q&A view and the reaction taps, to the
// presenter's screens only. Their shapes are in qa/types.ts.
// SW-8: a word-cloud or open-text slide's round, to guests and the big screen and to the presenter's screens. Their
// shapes are in openResponses/types.ts.
export type AudienceEvent =
  | AudienceState
  | AudienceEnded
  | JoinedCount
  | PollTally
  | PollControl
  | QuestionTally
  | QuestionControl
  | ReactionsBurst
  | WordCloudEvent
  | OpenTextEvent;

export function isAudienceEvent(value: unknown): value is AudienceEvent {
  if (typeof value !== 'object' || value === null) return false;
  const type = (value as { type?: unknown }).type;
  return (
    type === 'audienceState' ||
    type === 'audienceEnded' ||
    type === 'joinedCount' ||
    type === 'pollTally' ||
    type === 'pollControl' ||
    type === 'questionTally' ||
    type === 'questionControl' ||
    type === 'reactions' ||
    type === 'wordCloud' ||
    type === 'openText'
  );
}

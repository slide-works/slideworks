// The app's narrow views of the deck hub's wire (contract K3), camelCase as the server sends them. They are structural
// subsets: the SPA's full types (guest/audienceEvents.ts, presentation/) stay in the SPA and are assignable to them.

// The slide on screen: index is 1-based (Realtime/AudienceSlide.cs).
export type AudienceSlideSummary = { index: number; count: number };

// JoinAudience's answer and the audienceEvent that replaces it. There is no deckId on the wire.
export type AudienceStateSummary = {
  type: 'audienceState';
  seq: number;
  phase: 'lobby' | 'live' | 'ended';
  current: AudienceSlideSummary | null;
};

export type AudienceEndedSummary = { type: 'audienceEnded' };

// Other event types (polls, Q&A, reactions, …) are ignored by the app in v0.
export type AudienceEventSummary = AudienceStateSummary | AudienceEndedSummary | { type: string };

// slide is 1-based, step 0-based (Realtime/Presenting/PresentationState.cs).
export type PresentationStateSummary = { slide: number; step: number };

// JoinPresentation's answer.
export type PresentationJoinedSummary = { state: PresentationStateSummary };

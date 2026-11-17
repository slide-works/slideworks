// Mirrors src/SlideWorks.WebApi/Realtime/Presenting/ (contract P1): the presentation state every window and phone shares
// over DeckHub, and the hub calls' arguments and results (P2). The sample is src/contracts/presentation-state.json.
// Clients only render the state; they never keep a second position (D4).

export type PresentationRole = 'presenter' | 'audience' | 'phone';
export type TimerState = 'notStarted' | 'running' | 'paused';
export type TimerCommand = 'pause' | 'resume' | 'reset';

// elapsedMs is the server's reading when the message was sent. While running, a
// client adds the performance.now() delta since it received the message (D7).
export type PresentationTimer = { state: TimerState; elapsedMs: number };

// clientId is made once per page load; a rejoin with the same id replaces the
// entry. label is trimmed and capped at 60 characters by the server.
export type PresentationConnection = {
  clientId: string;
  role: PresentationRole;
  label: string;
};

// The "presentationState" message. Apply a state when its epoch differs from the
// last applied one, or when the epoch is the same and its seq is higher (P1).
// slide is 1-based and step 0-based, as the player's #slide.step (K12).
// connected: presenter windows, then audience windows, then phones, each in join
// order. Never carries notes (P9).
export type PresentationState = {
  deckId: string;
  epoch: string;
  seq: number;
  slide: number;
  step: number;
  black: boolean;
  skipDeepDives: boolean;
  timer: PresentationTimer;
  talkMinutes: number | null;
  connected: PresentationConnection[];
};

// UpdatePresentation's argument (P2). The server clamps the position.
export type PresentationUpdate = {
  slide: number;
  step: number;
  black: boolean;
  skipDeepDives: boolean;
};

// JoinPresentation's resume argument: the last applied state, used only when the
// server has none for the deck (D9).
export type PresentationResume = PresentationUpdate & { timer: PresentationTimer };

// JoinPresentation's result. clockMinutes is minutes since local midnight in the
// time zone the client sent (0-1439), advanced with performance.now() (D7).
export type PresentationJoined = { state: PresentationState; clockMinutes: number };

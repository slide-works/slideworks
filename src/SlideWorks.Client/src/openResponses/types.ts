// Mirrors src/contracts/screens/word-cloud.json, open-text.json, open-response-control.json, the "wordCloud" and
// "openText" samples in audience-events.json, and the records in Features/OpenResponses/Contracts (SW-8).

export type OpenResponseKind = 'word-cloud' | 'open-text';

export type OpenResponsePhase = 'draft' | 'open' | 'closed';

export type OpenResponseResults = 'live' | 'afterAnswer' | 'afterClose';

export type OpenResponseOpens = 'arrival' | 'manual';

// round is left out while phase is draft and present otherwise. number is the round's number, or in draft the number
// the next round will get.
export type OpenResponseRound =
  | { phase: 'draft'; round?: never }
  | { phase: 'open' | 'closed'; round: string };

// One word of a cloud: the first casing anyone sent, how many guests hold it now, and whether the presenter hid it.
export type CloudWord = { text: string; count: number; hidden: boolean };

// One answer on the wall: the guest's stable 1-based number, their text, and whether the presenter hid it.
export type WallAnswer = { number: number; text: string; hidden: boolean };

type OpenResponseEventFields = OpenResponseRound & {
  slide: number;
  number: number;
  revision: number;
  revealed: boolean;
  guests: number;
};

// A word-cloud slide's round, to guests and the big screen (hidden words left out) and to the presenter's screens.
export type WordCloudEvent = OpenResponseEventFields & { type: 'wordCloud'; words: CloudWord[] };

// An open-text slide's round, to guests and the big screen (hidden answers left out) and to the presenter's screens.
export type OpenTextEvent = OpenResponseEventFields & { type: 'openText'; answers: WallAnswer[] };

type GuestResponseFields = OpenResponseRound & {
  number: number;
  revision: number;
  question: string;
  revealed: boolean;
  guests: number;
};

// GET /api/join/{code}/slides/{slide}/response: the guest's own entry as stored ([] or "" before they send one).
export type GuestResponseView =
  | (GuestResponseFields & { kind: 'word-cloud'; mineWords: string[] })
  | (GuestResponseFields & { kind: 'open-text'; mineText: string });

// PUT /api/join/{code}/slides/{slide}/words and its 200 body.
export type WordsRequest = { round: string; revision: number; words: string[] };
export type StoredWords = { words: string[] };

// PUT /api/join/{code}/slides/{slide}/answer and its 200 body.
export type TextRequest = { round: string; revision: number; text: string };
export type StoredText = { text: string };

// One entry on the presenter's control view. count is a word's and is left out for an answer.
export type ControlEntry = { number: number; text: string; count?: number; hidden: boolean };

// GET /api/decks/{id}/slides/{slide}/response: every entry, hidden ones included.
export type OpenResponseControlView = OpenResponseRound & {
  kind: OpenResponseKind;
  number: number;
  revision: number;
  results: OpenResponseResults;
  opens: OpenResponseOpens;
  revealed: boolean;
  guests: number;
  entries: ControlEntry[];
};

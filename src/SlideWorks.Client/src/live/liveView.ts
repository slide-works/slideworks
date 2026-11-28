import type { ReadingInCount } from '../guest/audienceEvents';

// GET, POST /api/decks/{id}/live (SW-5 J2): the deck's live session as the owner sees it. Mirrors
// src/contracts/screens/live.json. sessionId is null while no session is open. readingIn is the guests connected now by
// the language they read (SW-886 K11), null while translations are off or the deck is not live.
export type LiveView = {
  sessionId: string | null;
  code: string;
  codeLabel: string;
  joinUrl: string;
  hostLabel: string;
  live: boolean;
  joinedCount: number;
  joinBar: boolean;
  readingIn: ReadingInCount[] | null;
};

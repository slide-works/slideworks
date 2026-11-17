// GET, POST /api/decks/{id}/live (SW-5 J2): the deck's live session as the owner sees it. Mirrors
// src/contracts/screens/live.json. sessionId is null while no session is open.
export type LiveView = {
  sessionId: string | null;
  code: string;
  codeLabel: string;
  joinUrl: string;
  hostLabel: string;
  live: boolean;
  joinedCount: number;
  joinBar: boolean;
};

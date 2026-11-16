// Mirrors the API's view models (contract K4), one per screen. Their JSON examples
// are src/contracts/screens/*.json at the repo's src/ level. Dates arrive
// pre-formatted (`updatedLabel`, `atLabel`), never as ISO strings for the browser
// to parse — see .claude/skills/no-browser-datetime.
import type { LayoutName, Slots } from '../slides/slots';

export type ActorKind = 'agent' | 'person';
export type Via = 'mcp' | 'web';
export type SharingLevel = 'private' | 'link' | 'named';

// GET /api/me
export type MeView = {
  email: string;
  name: string;
  pictureUrl: string | null;
  access: 'pending' | 'approved';
  isAdmin: boolean;
  pendingRequests: number;
  adminMissing: boolean;
};

// GET /api/signin/options: what the sign-in page offers. google is false while
// sign-in isn't set up on the server.
export type SignInOptionsView = { google: boolean };

// GET /api/admin/users
export type AdminUserRow = {
  id: string;
  name: string;
  email: string;
  pictureUrl: string | null;
  access: 'pending' | 'approved' | 'rejected';
  isAdmin: boolean;
  isSelf: boolean;
  requestedLabel: string;
  changedLabel: string | null;
  changedBy: string | null;
};

export type AdminUsersView = { users: AdminUserRow[]; pendingCount: number; adminCount: number };

export type LastChange = { actor: string; actorKind: ActorKind; via: Via; summary: string };

export type DeckRow = {
  id: string;
  slug: string;
  title: string;
  subtitle: string | null;
  slideCount: number;
  lastChange: LastChange | null;
  sharing: SharingLevel;
  viewerCount: number;
  updatedLabel: string;
  playUrl: string;
};

export type SharedDeckRow = DeckRow & { ownerName: string };

// GET /api/decks
export type DecksLibraryView = {
  decks: DeckRow[];
  sharedWithMe: SharedDeckRow[];
  connect: { show: boolean; mcpUrl: string };
};

// One variant per layout, so narrowing on `layout` narrows `slots`.
type WithSlots<Common> = {
  [L in LayoutName]: Common & { layout: L; slots: Slots[L] };
}[LayoutName];

export type DeckPageSlide = WithSlots<{
  number: number;
  part: number | null;
  headline: string | null;
  steps: number;
  notes: string | null;
  link: string;
}>;

export type ActivityRow = {
  actor: string;
  actorKind: ActorKind;
  via: Via;
  tool: string | null;
  summary: string;
  atLabel: string;
};

// GET /api/decks/{id}. Activity is newest first, at most 50 entries.
export type DeckPageView = {
  id: string;
  slug: string;
  title: string;
  author: string | null;
  isOwner: boolean;
  playUrl: string;
  sharing: { level: SharingLevel; viewerCount: number };
  parts: string[];
  slides: DeckPageSlide[];
  activity: ActivityRow[];
};

// Never carries speaker notes.
export type DeckPlaySlide = WithSlots<{ number: number; part: number | null; steps: number }>;

// GET /api/play/{id}, and the shape of src/contracts/sample-deck.json.
export type DeckPlayView = {
  id: string;
  slug: string;
  title: string;
  author: string | null;
  parts: string[];
  slides: DeckPlaySlide[];
};

export type SharingChange = { actor: string; actorKind: ActorKind; atLabel: string };

// GET and PUT /api/decks/{id}/sharing
export type SharingView = {
  level: SharingLevel;
  viewers: string[];
  link: string;
  lastChange: SharingChange | null;
};

// The PUT /api/decks/{id}/sharing body.
export type SharingRequest = { level: SharingLevel; viewers: string[] };

// POST /api/decks body, and its 201 result.
export type CreateDeckRequest = { title: string };
export type CreateDeckResult = { id: string; slug: string; playUrl: string };

// GET /api/oauth/consent/{id}: the consent screen for an agent's authorize request.
export type ConsentView = { clientName: string; scope: string };

// POST /api/oauth/consent/{id} body, and its result: where the browser goes next.
export type ConsentDecisionRequest = { approve: boolean };
export type ConsentDecisionView = { redirect: string };

// Every error body.
export type ApiErrorBody = { error: string };

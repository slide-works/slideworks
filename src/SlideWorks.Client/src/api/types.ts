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
  deepDive: boolean;
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
  join: JoinView;
  sharing: { level: SharingLevel; viewerCount: number };
  revision: number;
  look: DeckPageLook;
  parts: string[];
  slides: DeckPageSlide[];
  activity: ActivityRow[];
};

// Never carries speaker notes.
export type DeckPlaySlide = WithSlots<{
  number: number;
  part: number | null;
  deepDive: boolean;
  steps: number;
}>;

// How the room reaches a deck (SW-5 J1), on the play view, the deck page and the render page. code is the deck's
// standing six digits, codeLabel the same in two triples, joinUrl what the QR code holds, hostLabel what a slide
// prints, and live whether a session is open.
export type JoinView = {
  code: string;
  codeLabel: string;
  joinUrl: string;
  hostLabel: string;
  live: boolean;
};

// GET /api/play/{id}, and the shape of src/contracts/sample-deck.json.
export type DeckPlayView = {
  id: string;
  slug: string;
  title: string;
  author: string | null;
  parts: string[];
  look: LookView;
  join: JoinView;
  // True only for the signed-in person who owns the deck; the player asks the live session only when it is true.
  isOwner: boolean;
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
export type CreateDeckRequest = { title: string; template?: string; palette?: string };
export type CreateDeckResult = { id: string; slug: string; playUrl: string };

// GET /api/oauth/consent/{id}: the consent screen for an agent's authorize request.
// returnsTo is where the answer goes: a redirect's host and port (localhost:33418) or an
// app's own scheme (cursor://anysphere.cursor-mcp). clientName is self-chosen; returnsTo isn't.
export type ConsentView = { clientName: string; scope: string; returnsTo: string };

// POST /api/oauth/consent/{id} body, and its result: where the browser goes next.
export type ConsentDecisionRequest = { approve: boolean };
export type ConsentDecisionView = { redirect: string };

// The look (SW-2 L6): a deck's template and palette, resolved by the server.
// Palette token keys are the --sw-<key> CSS custom properties without the
// prefix (D10); the server always sends every token resolved, so the client
// never looks a palette up by name.
export type PaletteTokenKey =
  | 'paper'
  | 'card'
  | 'chip'
  | 'grid'
  | 'ink'
  | 'muted'
  | 'accent'
  | 'accent-2'
  | 'accent-dark'
  | 'accent-light'
  | 'accent-glow'
  | 'accent-past'
  | 'on-accent'
  | 'good'
  | 'good-soft'
  | 'good-ink'
  | 'bad'
  | 'bad-soft'
  | 'bad-ink'
  | 'warn'
  | 'warn-soft'
  | 'warn-ink'
  | 'dd'
  | 'on-dd'
  | 'dd-on-night'
  | 'night'
  | 'night-ink'
  | 'night-2'
  | 'night-muted'
  | 'night-line'
  | 'night-glow'
  | 'number-from'
  | 'number-to'
  | 'stamp'
  | 'panel-accent'
  | 'panel-accent-ink'
  | 'panel-positive'
  | 'panel-positive-ink'
  | 'panel-negative'
  | 'panel-negative-ink'
  | 'panel-inverse'
  | 'panel-inverse-ink';

export type PaletteTokens = Record<PaletteTokenKey, string>;

export type FooterMode = 'author' | 'date' | 'off';

export type TemplateChrome = {
  tracker: boolean;
  footer: FooterMode;
  progress: boolean;
  deepDivesOnDivider: boolean;
  draft: boolean;
};

// head and body are font family names; headTracking is a CSS length.
export type TemplateType = { head: string; body: string; headWeight: number; headTracking: string };

// Radii in px; shadow is a CSS box-shadow.
export type TemplateShape = {
  radius: { sm: number; md: number; lg: number; xl: number };
  shadow: string;
};

export type Ornament = 'dots' | 'none';

// A deck's template in a LookView. chrome is the effective chrome, after the
// deck's overrides.
export type LookTemplate = {
  name: string;
  label: string;
  layouts: LayoutName[];
  chrome: TemplateChrome;
  type: TemplateType;
  shape: TemplateShape;
  ornament: Ornament;
  titleArt: string | null;
};

export type LookPalette = { name: string; label: string; tokens: PaletteTokens };

// Image URLs are root-relative (/api/images/{id}).
export type LookView = {
  template: LookTemplate;
  palette: LookPalette;
  logo: { url: string } | null;
  draft: boolean;
  footerDateLabel: string;
  // Whether the join bar (the small QR and code on every slide while live) is on (SW-5 J6).
  joinBar: boolean;
};

// A deck's own chrome settings over its template's; a missing or null key
// means the template's default.
export type ChromeOverrides = {
  tracker?: boolean | null;
  footer?: FooterMode | null;
  progress?: boolean | null;
  // Plain boolean, off by default (SW-5 D5): not an override of a template value.
  joinBar?: boolean;
};

export type ContrastPair =
  | 'body'
  | 'secondary'
  | 'card-bold'
  | 'headline-accent'
  | 'on-accent'
  | 'on-accent-2'
  | 'deep-dive-pill'
  | 'panel-accent'
  | 'panel-positive'
  | 'panel-negative'
  | 'panel-inverse'
  | 'badge-positive'
  | 'badge-negative'
  | 'dark-subtitle'
  | 'soft-accent'
  | 'soft-inverse'
  | 'deep-dive-list';

// A palette's contrast check: whether every pair meets its bar, and the pair
// closest to (or furthest below) its bar.
export type ContrastSummary = {
  pass: boolean;
  lowest: { pair: ContrastPair; ratio: number; bar: number };
};

// The deck page's look: the LookView plus the deck's overrides and its palette's contrast.
export type DeckPageLook = LookView & {
  chromeOverrides: ChromeOverrides;
  contrast: ContrastSummary;
};

// GET /api/looks: the built-in templates and palettes, the same for everyone (D20).
export type LooksTemplate = {
  name: string;
  label: string;
  purpose: string;
  isDefault: boolean;
  layouts: LayoutName[];
  chrome: TemplateChrome;
  outline: string[];
  type: TemplateType;
  shape: TemplateShape;
  ornament: Ornament;
  titleArt: string | null;
  // The palette a locking template forces, or null.
  lockedPalette: string | null;
};

export type LooksPalette = {
  name: string;
  label: string;
  isDefault: boolean;
  swatch: string[];
  tokens: PaletteTokens;
  contrast: ContrastSummary;
};

export type LooksView = { templates: LooksTemplate[]; palettes: LooksPalette[] };

// The PUT /api/decks/{id}/look body. Each key left out stays as it is; a null
// logo removes it, and a null chrome key resets it to the template's default.
export type LookChangeRequest = {
  template?: string;
  palette?: string;
  logo?: string | null;
  draft?: boolean;
  chrome?: ChromeOverrides;
};

// Its 200 result. warnings name slides whose layout the template doesn't list.
export type LookChangeResult = { look: DeckPageLook; warnings: string[] };

// GET /api/render/{id}/card. A generic card has a null title, subtitle and author.
export type CardView = {
  generic: boolean;
  title: string | null;
  subtitle: string | null;
  author: string | null;
  slideCount: number;
  look: LookView;
};

// POST /api/decks/{id}/images body, and its 201 result (the same as upload_image's).
export type ImageUploadRequest = { name: string; contentType: string };
export type ImageUploadResult = {
  image: string;
  uploadUrl: string;
  method: 'PUT';
  expiresInSeconds: number;
};

// Every error body.
export type ApiErrorBody = { error: string };

// SW-4's screens, appended (P3, P4).
import type { PresentationRole } from '../presentation/types';

// GET /api/decks/{id}/presenter (P3), owner only: the play view's fields plus
// each slide's notes (plain text: "**bold**" and "- " lines are the only markup,
// rendered by NotesText) and headline, the talk length, and the audience and
// phone URLs (absolute).
export type PresenterSlide = WithSlots<{
  number: number;
  part: number | null;
  deepDive: boolean;
  steps: number;
  notes: string | null;
  headline: string | null;
}>;

export type PresenterView = {
  id: string;
  slug: string;
  title: string;
  author: string | null;
  parts: string[];
  look: LookView;
  join: JoinView;
  slides: PresenterSlide[];
  talkMinutes: number | null;
  audienceUrl: string;
  remoteUrl: string;
};

// GET /api/presentations (P4): the signed-in person's live presentations, newest
// first. Text only; startedLabel is pre-formatted by the server.
export type LiveWindow = { role: PresentationRole; label: string };

export type LivePresentation = {
  deckId: string;
  title: string;
  slide: number;
  total: number;
  startedLabel: string;
  windows: LiveWindow[];
};

export type LivePresentationsView = { presentations: LivePresentation[] };

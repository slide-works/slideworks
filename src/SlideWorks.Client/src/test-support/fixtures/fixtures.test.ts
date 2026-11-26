import { describe, expect, it } from 'vitest';
import type {
  ActivityRow,
  AdminUserRow,
  AdminUsersView,
  CardView,
  ChromeOverrides,
  ContrastPair,
  ContrastSummary,
  DeckPageLook,
  DeckPageSlide,
  DeckPageView,
  DeckPlaySlide,
  DeckPlayView,
  DeckRow,
  DecksLibraryView,
  ImageUploadRequest,
  ImageUploadResult,
  JoinView,
  LastChange,
  LookChangeRequest,
  LookChangeResult,
  LookPalette,
  LookTemplate,
  LookView,
  LooksPalette,
  LooksTemplate,
  LooksView,
  MeView,
  PaletteTokenKey,
  SharedDeckRow,
  SharingChange,
  SharingView,
  TemplateChrome,
  TemplateShape,
  TemplateType,
} from '../../api/types';
import { LAYOUTS } from '../../slides/layouts';
import {
  LAYOUT_NAMES,
  SLOT_KEYS,
  type CalloutBlock,
  type GanttRow,
  type LayoutName,
  type PracticeSlots,
} from '../../slides/slots';
import { LAYOUT_SLOTS } from './layoutSlots';
import {
  SAMPLE_SLOTS,
  adminUsers,
  card,
  deckPage,
  deckPlay,
  decksLibrary,
  imageUploadRequest,
  imageUploadResult,
  lookChangeRequest,
  lookChangeResult,
  looks,
  me,
  sampleDeck,
  sharing,
  deckPrint,
} from './screens';

// The run-time half of the fixture check: `satisfies` in screens.ts checks the
// field names and value kinds outside the slots; these check the exact key sets,
// the literal values, and the slots against the sample deck. Each table is a
// Record over the type's keys, so it can't miss or invent a key either.
const keys = <T>(table: Record<keyof T, true>) => Object.keys(table).sort();
const ME = keys<MeView>({
  email: true,
  name: true,
  pictureUrl: true,
  avatar: true,
  timeZone: true,
  access: true,
  isAdmin: true,
  pendingRequests: true,
  adminMissing: true,
});
const ADMIN_USERS = keys<AdminUsersView>({ users: true, pendingCount: true, adminCount: true });
const ADMIN_USER_ROW = keys<AdminUserRow>({
  id: true,
  name: true,
  email: true,
  pictureUrl: true,
  avatar: true,
  access: true,
  isAdmin: true,
  isSelf: true,
  requestedLabel: true,
  changedLabel: true,
  changedBy: true,
});
const LAST_CHANGE = keys<LastChange>({ actor: true, actorKind: true, via: true, summary: true });
const DECK_ROW = keys<DeckRow>({
  id: true,
  slug: true,
  title: true,
  subtitle: true,
  slideCount: true,
  lastChange: true,
  sharing: true,
  viewerCount: true,
  updatedLabel: true,
  playUrl: true,
});
const SHARED_DECK_ROW = keys<SharedDeckRow>({
  id: true,
  slug: true,
  title: true,
  subtitle: true,
  slideCount: true,
  lastChange: true,
  sharing: true,
  viewerCount: true,
  updatedLabel: true,
  playUrl: true,
  ownerName: true,
  access: true,
});
const LIBRARY = keys<DecksLibraryView>({ decks: true, sharedWithMe: true, connect: true });
const DECK_PAGE = keys<DeckPageView>({
  id: true,
  slug: true,
  title: true,
  author: true,
  isOwner: true,
  canEdit: true,
  access: true,
  playUrl: true,
  join: true,
  sharing: true,
  revision: true,
  look: true,
  parts: true,
  slides: true,
  activity: true,
  findings: true,
});
const DECK_PAGE_SLIDE = keys<DeckPageSlide>({
  number: true,
  layout: true,
  part: true,
  deepDive: true,
  surface: true,
  headline: true,
  steps: true,
  notes: true,
  link: true,
  slots: true,
});
const ACTIVITY = keys<ActivityRow>({
  actor: true,
  actorKind: true,
  via: true,
  tool: true,
  summary: true,
  atLabel: true,
  isMe: true,
});
const DECK_PLAY = keys<DeckPlayView>({
  id: true,
  slug: true,
  title: true,
  author: true,
  parts: true,
  look: true,
  join: true,
  isOwner: true,
  slides: true,
});
const JOIN = keys<JoinView>({
  code: true,
  codeLabel: true,
  joinUrl: true,
  hostLabel: true,
  live: true,
});
const DECK_PLAY_SLIDE = keys<DeckPlaySlide>({
  number: true,
  layout: true,
  part: true,
  deepDive: true,
  surface: true,
  steps: true,
  slots: true,
});
const SHARING = keys<SharingView>({
  level: true,
  viewers: true,
  link: true,
  lastChange: true,
  owner: true,
  people: true,
});
const SHARING_CHANGE = keys<SharingChange>({ actor: true, actorKind: true, atLabel: true });
const LOOK = keys<LookView>({
  template: true,
  palette: true,
  logo: true,
  fonts: true,
  logos: true,
  art: true,
  ornament: true,
  topBar: true,
  templateRef: true,
  templateVersion: true,
  latestVersion: true,
  extras: true,
  customLayouts: true,
  templateMissing: true,
  draft: true,
  footerDateLabel: true,
  joinBar: true,
});
const DECK_PAGE_LOOK = keys<DeckPageLook>({
  template: true,
  palette: true,
  logo: true,
  fonts: true,
  logos: true,
  art: true,
  ornament: true,
  topBar: true,
  templateRef: true,
  templateVersion: true,
  latestVersion: true,
  extras: true,
  customLayouts: true,
  templateMissing: true,
  draft: true,
  footerDateLabel: true,
  joinBar: true,
  chromeOverrides: true,
  contrast: true,
});
const LOOK_TEMPLATE = keys<LookTemplate>({
  name: true,
  label: true,
  layouts: true,
  chrome: true,
  type: true,
  shape: true,
  ornament: true,
  titleArt: true,
});
const LOOK_PALETTE = keys<LookPalette>({ name: true, label: true, tokens: true });
const CHROME = keys<TemplateChrome>({
  tracker: true,
  footer: true,
  progress: true,
  deepDivesOnDivider: true,
  draft: true,
});
const CHROME_OVERRIDES = keys<ChromeOverrides>({
  tracker: true,
  footer: true,
  progress: true,
  joinBar: true,
  topBar: true,
});
const TEMPLATE_TYPE = keys<TemplateType>({
  head: true,
  body: true,
  headWeight: true,
  headTracking: true,
});
const TEMPLATE_SHAPE = keys<TemplateShape>({ radius: true, shadow: true });
const TOKENS = keys<Record<PaletteTokenKey, string>>({
  paper: true,
  card: true,
  chip: true,
  grid: true,
  ink: true,
  muted: true,
  accent: true,
  'accent-2': true,
  'accent-dark': true,
  'accent-light': true,
  'accent-glow': true,
  'accent-past': true,
  'on-accent': true,
  good: true,
  'good-soft': true,
  'good-ink': true,
  bad: true,
  'bad-soft': true,
  'bad-ink': true,
  warn: true,
  'warn-soft': true,
  'warn-ink': true,
  dd: true,
  'on-dd': true,
  'dd-on-night': true,
  night: true,
  'night-ink': true,
  'night-2': true,
  'night-muted': true,
  'night-line': true,
  'night-glow': true,
  'number-from': true,
  'number-to': true,
  stamp: true,
  'panel-accent': true,
  'panel-accent-ink': true,
  'panel-positive': true,
  'panel-positive-ink': true,
  'panel-negative': true,
  'panel-negative-ink': true,
  'panel-inverse': true,
  'panel-inverse-ink': true,
});
const CONTRAST = keys<ContrastSummary>({ pass: true, lowest: true });
const CONTRAST_PAIRS = keys<Record<ContrastPair, true>>({
  body: true,
  secondary: true,
  'card-bold': true,
  'headline-accent': true,
  'on-accent': true,
  'on-accent-2': true,
  'deep-dive-pill': true,
  'panel-accent': true,
  'panel-positive': true,
  'panel-negative': true,
  'panel-inverse': true,
  'badge-positive': true,
  'badge-negative': true,
  'dark-subtitle': true,
  'soft-accent': true,
  'soft-inverse': true,
  'deep-dive-list': true,
  'dark-kicker': true,
  'dark-chrome': true,
  'dark-soft-accent': true,
  'dark-soft-positive': true,
  'dark-soft-negative': true,
  'pin-solid-accent': true,
  'pin-solid-positive': true,
  'pin-solid-negative': true,
});
const LOOKS = keys<LooksView>({ templates: true, palettes: true });
const LOOKS_TEMPLATE = keys<LooksTemplate>({
  name: true,
  label: true,
  purpose: true,
  isDefault: true,
  layouts: true,
  chrome: true,
  outline: true,
  type: true,
  shape: true,
  ornament: true,
  titleArt: true,
  lockedPalette: true,
  ref: true,
  version: true,
  builtIn: true,
  owner: true,
});
const LOOKS_PALETTE = keys<LooksPalette>({
  name: true,
  label: true,
  isDefault: true,
  swatch: true,
  tokens: true,
  contrast: true,
});
const LOOK_CHANGE_REQUEST = keys<LookChangeRequest>({
  template: true,
  templateVersion: true,
  palette: true,
  logo: true,
  draft: true,
  chrome: true,
});
const LOOK_CHANGE_RESULT = keys<LookChangeResult>({ look: true, warnings: true });
const CARD = keys<CardView>({
  generic: true,
  kicker: true,
  title: true,
  subtitle: true,
  author: true,
  slideCount: true,
  host: true,
  look: true,
});
const IMAGE_UPLOAD_REQUEST = keys<ImageUploadRequest>({ name: true, contentType: true });
const IMAGE_UPLOAD_RESULT = keys<ImageUploadResult>({
  image: true,
  uploadUrl: true,
  method: true,
  expiresInSeconds: true,
});

const sorted = (value: object) => Object.keys(value).sort();
const ACTOR_KINDS = ['agent', 'person'];
const VIAS = ['mcp', 'web'];
const LEVELS = ['private', 'link', 'named'];
const DECK_ID = /^[0-9A-Za-z]{12}$/;
const FOOTERS = ['author', 'date', 'off'];
const ORNAMENTS = ['dots', 'none'];
const IMAGE_URL = /^\/api\/images\/[^/]+$/;

function nullPaths(value: unknown, path = '$'): string[] {
  if (value === null) return [path];
  if (Array.isArray(value)) return value.flatMap((item, i) => nullPaths(item, `${path}[${i}]`));
  if (typeof value === 'object')
    return Object.entries(value).flatMap(([key, item]) => nullPaths(item, `${path}.${key}`));
  return [];
}

// What a built-in template leaves null in a look (T7), in the order the look holds them; the server's
// ScreenShapes.BuiltInLookNullablePaths declares the same seven: a built-in has no title art either. The deck samples show a built-in deck as the server sends it, so
// these are their only nulls; template-public.json is the sample of an uploaded template, with every one filled.
const LOOK_NULLABLE = [
  'template.titleArt',
  'logos.light',
  'logos.dark',
  'art.generator',
  'art.url',
  'templateVersion',
  'latestVersion',
];
const builtInLookNulls = LOOK_NULLABLE.map((member) => `$.look.${member}`);
// The deck samples' four parts have no caption and no mark (K19), so those are null too, ahead of the look.
const partNulls = [0, 1, 2, 3].flatMap((i) => [`$.parts[${i}].caption`, `$.parts[${i}].mark`]);

function expectSlots(layout: string, slots: object) {
  expect(LAYOUT_NAMES).toContain(layout);
  const allowed: readonly string[] = SLOT_KEYS[layout as LayoutName];
  for (const key of Object.keys(slots)) expect(allowed, `${layout}.${key}`).toContain(key);
}

function expectLastChange(change: LastChange | null) {
  expect(sorted(change!)).toEqual(LAST_CHANGE);
  expect(ACTOR_KINDS).toContain(change!.actorKind);
  expect(VIAS).toContain(change!.via);
}

function expectChrome(chrome: TemplateChrome) {
  expect(sorted(chrome)).toEqual(CHROME);
  expect(FOOTERS).toContain(chrome.footer);
}

function expectTemplate(template: LookTemplate | LooksTemplate, expected: string[]) {
  expect(sorted(template)).toEqual(expected);
  for (const layout of template.layouts) expect(LAYOUT_NAMES).toContain(layout);
  expectChrome(template.chrome);
  expect(sorted(template.type)).toEqual(TEMPLATE_TYPE);
  expect(sorted(template.shape)).toEqual(TEMPLATE_SHAPE);
  expect(sorted(template.shape.radius)).toEqual(['lg', 'md', 'sm', 'xl']);
  expect(ORNAMENTS).toContain(template.ornament);
}

function expectContrast(contrast: ContrastSummary) {
  expect(sorted(contrast)).toEqual(CONTRAST);
  expect(sorted(contrast.lowest)).toEqual(['bar', 'pair', 'ratio']);
  expect(CONTRAST_PAIRS).toContain(contrast.lowest.pair);
}

// A LookView, or the deck page's look with its overrides and contrast.
function expectLook(look: LookView, expected: string[] = LOOK) {
  expect(sorted(look)).toEqual(expected);
  expectTemplate(look.template, LOOK_TEMPLATE);
  expect(sorted(look.palette)).toEqual(LOOK_PALETTE);
  expect(sorted(look.palette.tokens)).toEqual(TOKENS);
  // Root-relative, so a headless browser loads it from the host it renders on.
  if (look.logo !== null) {
    expect(sorted(look.logo)).toEqual(['url']);
    expect(look.logo.url).toMatch(IMAGE_URL);
  }
}

// The join object (SW-5 J1): a six-digit code, the same in two triples, and an https link that ends in it.
function expectJoin(join: JoinView) {
  expect(sorted(join)).toEqual(JOIN);
  expect(join.code).toMatch(/^[1-9]\d{5}$/);
  expect(join.codeLabel).toBe(`${join.code.slice(0, 3)} ${join.code.slice(3)}`);
  expect(join.joinUrl.endsWith(join.code)).toBe(true);
  expect(typeof join.live).toBe('boolean');
}

function expectDeckPageLook(look: DeckPageLook) {
  expectLook(look, DECK_PAGE_LOOK);
  for (const key of Object.keys(look.chromeOverrides)) expect(CHROME_OVERRIDES).toContain(key);
  if (look.chromeOverrides.footer != null) expect(FOOTERS).toContain(look.chromeOverrides.footer);
  expectContrast(look.contrast);
}

function expectDeckRow(row: DeckRow, expected: string[]) {
  expect(sorted(row)).toEqual(expected);
  expect(row.id).toMatch(DECK_ID);
  expect(LEVELS).toContain(row.sharing);
  expectLastChange(row.lastChange);
}

describe('screen JSON examples', () => {
  it('give every nullable field a non-null value', () => {
    for (const screen of [me, adminUsers, decksLibrary]) expect(nullPaths(screen)).toEqual([]);
    // The one person with no account has no name or avatar (D24); everything else is filled.
    expect(nullPaths(sharing)).toEqual(['$.people[1].name', '$.people[1].avatar']);
    for (const screen of [deckPage, deckPlay])
      expect(nullPaths(screen)).toEqual([...partNulls, ...builtInLookNulls]);
  });

  it('me.json matches MeView', () => {
    expect(sorted(me)).toEqual(ME);
    expect(sorted(me.avatar)).toEqual(['colour', 'initials', 'pictureUrl']);
    expect(me.timeZone).toBe('Europe/London');
    expect(me.access).toBe('approved');
    expect(me.isAdmin).toBe(false);
    expect(me.pendingRequests).toBe(0);
    expect(me.adminMissing).toBe(false);
  });

  it('admin-users.json matches AdminUsersView', () => {
    expect(sorted(adminUsers)).toEqual(ADMIN_USERS);
    adminUsers.users.forEach((row) => {
      expect(sorted(row)).toEqual(ADMIN_USER_ROW);
      expect(['pending', 'approved', 'rejected']).toContain(row.access);
    });
    expect(adminUsers.users.map((row) => row.access)).toEqual(
      expect.arrayContaining(['pending', 'approved', 'rejected']),
    );
    expect(adminUsers.users.some((row) => row.access === 'approved' && row.isAdmin)).toBe(true);
    expect(adminUsers.pendingCount).toBe(
      adminUsers.users.filter((r) => r.access === 'pending').length,
    );
    expect(adminUsers.adminCount).toBe(adminUsers.users.filter((r) => r.isAdmin).length);
  });

  it('decks-library.json matches DecksLibraryView', () => {
    expect(sorted(decksLibrary)).toEqual(LIBRARY);
    expect(decksLibrary.decks.length).toBeGreaterThan(0);
    expect(decksLibrary.sharedWithMe.length).toBeGreaterThan(0);
    decksLibrary.decks.forEach((row) => expectDeckRow(row, DECK_ROW));
    decksLibrary.sharedWithMe.forEach((row) => expectDeckRow(row, SHARED_DECK_ROW));
    expect(sorted(decksLibrary.connect)).toEqual(['mcpUrl', 'show']);
  });

  it('deck-page.json matches DeckPageView', () => {
    expect(sorted(deckPage)).toEqual(DECK_PAGE);
    expect(deckPage.id).toMatch(DECK_ID);
    expect(sorted(deckPage.sharing)).toEqual(['level', 'viewerCount']);
    expect(LEVELS).toContain(deckPage.sharing.level);
    expect(deckPage.revision).toBeGreaterThan(0);
    expectDeckPageLook(deckPage.look);
    expectJoin(deckPage.join);
    expect(deckPage.slides.some((slide) => slide.deepDive)).toBe(true);
    for (const slide of deckPage.slides) {
      expect(sorted(slide)).toEqual(DECK_PAGE_SLIDE);
      expectSlots(slide.layout, slide.slots);
    }
    for (const entry of deckPage.activity) {
      expect(sorted(entry)).toEqual(ACTIVITY);
      expect(ACTOR_KINDS).toContain(entry.actorKind);
      expect(VIAS).toContain(entry.via);
    }
  });

  it('deck-play.json matches DeckPlayView: title, practice and close, with no notes', () => {
    expect(sorted(deckPlay)).toEqual(DECK_PLAY);
    expect(deckPlay.id).toMatch(DECK_ID);
    expect(deckPlay.slides.map((slide) => slide.layout)).toEqual(['title', 'practice', 'close']);
    expectLook(deckPlay.look);
    expectJoin(deckPlay.join);
    expect(deckPlay.slides.some((slide) => slide.deepDive)).toBe(true);
    for (const slide of deckPlay.slides) {
      expect(sorted(slide)).toEqual(DECK_PLAY_SLIDE);
      expectSlots(slide.layout, slide.slots);
    }
  });

  it('deck-print.json matches DeckPrintView: the play view and a non-empty example at every array path', () => {
    expect(sorted(deckPrint)).toEqual(['deck', 'openTexts', 'polls', 'qa', 'wordClouds']);
    expect(sorted(deckPrint.deck)).toEqual(DECK_PLAY);
    expect(deckPrint.deck.id).toMatch(DECK_ID);
    expect(deckPrint.deck.slides.map((slide) => slide.layout)).toEqual([
      'title',
      'poll',
      'word-cloud',
      'open-text',
      'qa',
    ]);
    for (const slide of deckPrint.deck.slides) expectSlots(slide.layout, slide.slots);
    expect(deckPrint.polls.length).toBeGreaterThan(0);
    expect(deckPrint.wordClouds.length).toBeGreaterThan(0);
    expect(deckPrint.openTexts.length).toBeGreaterThan(0);
    expect(deckPrint.qa?.top.length).toBeGreaterThan(0);
    expect(deckPrint.polls.map((poll) => poll.slide)).toEqual([2]);
    expect(deckPrint.wordClouds.map((cloud) => cloud.slide)).toEqual([3]);
    expect(deckPrint.openTexts.map((text) => text.slide)).toEqual([4]);
  });

  it('sample-deck.json matches DeckPlayView, with a look and a deep dive', () => {
    expect(sorted(sampleDeck)).toEqual(DECK_PLAY);
    expectLook(sampleDeck.look);
    expectJoin(sampleDeck.join);
    expect(sampleDeck.slides.some((slide) => slide.deepDive)).toBe(true);
    for (const slide of sampleDeck.slides) {
      expect(sorted(slide)).toEqual(DECK_PLAY_SLIDE);
      expectSlots(slide.layout, slide.slots);
    }
  });

  it('looks.json matches LooksView', () => {
    // The first template is a made-up locking one with every field set; the
    // built-ins after it are the real catalogue, whose nulls are real.
    expect(nullPaths(looks.templates[0])).toEqual([]);
    expect(nullPaths(looks.palettes)).toEqual([]);
    expect(sorted(looks)).toEqual(LOOKS);
    expect(looks.templates.length).toBeGreaterThan(0);
    expect(looks.palettes.length).toBeGreaterThan(0);
    for (const template of looks.templates) {
      expectTemplate(template, LOOKS_TEMPLATE);
      expect(template.outline.length).toBeGreaterThan(0);
    }
    for (const palette of looks.palettes) {
      expect(sorted(palette)).toEqual(LOOKS_PALETTE);
      expect(sorted(palette.tokens)).toEqual(TOKENS);
      expect(palette.swatch.length).toBeGreaterThan(0);
      expectContrast(palette.contrast);
    }
  });

  it('look-change.json holds a LookChangeRequest and a LookChangeResult', () => {
    expect(nullPaths(lookChangeRequest)).toEqual([]);
    expect(nullPaths(lookChangeResult)).toEqual(builtInLookNulls);
    expect(sorted(lookChangeRequest)).toEqual(LOOK_CHANGE_REQUEST);
    for (const key of Object.keys(lookChangeRequest.chrome!))
      expect(CHROME_OVERRIDES).toContain(key);
    expect(sorted(lookChangeResult)).toEqual(LOOK_CHANGE_RESULT);
    expectDeckPageLook(lookChangeResult.look);
    expect(lookChangeResult.warnings.length).toBeGreaterThan(0);
  });

  it('card.json matches CardView', () => {
    expect(nullPaths(card)).toEqual(builtInLookNulls);
    expect(sorted(card)).toEqual(CARD);
    expect(card.generic).toBe(false);
    expectLook(card.look);
  });

  it('image-upload.json holds an ImageUploadRequest and an ImageUploadResult', () => {
    expect(sorted(imageUploadRequest)).toEqual(IMAGE_UPLOAD_REQUEST);
    expect(sorted(imageUploadResult)).toEqual(IMAGE_UPLOAD_RESULT);
    expect(imageUploadResult.method).toBe('PUT');
  });

  it('sharing.json matches SharingView', () => {
    expect(sorted(sharing)).toEqual(SHARING);
    expect(LEVELS).toContain(sharing.level);
    expect(sorted(sharing.lastChange!)).toEqual(SHARING_CHANGE);
    expect(ACTOR_KINDS).toContain(sharing.lastChange!.actorKind);
  });
});

// tsc can't check slots in JSON (see JsonView), so each example slide's slots
// must equal the sample deck's slide of the same layout. The sample's slides
// pass the server's SlideValidator, which makes the server the source of truth.
// When the sample changes, copy its slots into the screens, never the reverse.
function expectSampleSlots(slides: { layout: LayoutName; slots: object }[], file: string) {
  for (const slide of slides) {
    const sample = sampleDeck.slides.filter((s) => s.layout === slide.layout);
    expect(sample.length, `${file}: the sample deck has a ${slide.layout} slide`).toBeGreaterThan(
      0,
    );
    expect(
      sample.map((s) => s.slots),
      `${file}: ${slide.layout} slots equal a sample-deck ${slide.layout} slide`,
    ).toContainEqual(slide.slots);
  }
}

describe('slots in the screen JSON', () => {
  it('deck-play.json slides carry the sample deck slots of their layout', () => {
    expectSampleSlots(deckPlay.slides, 'deck-play.json');
  });

  it('deck-page.json slides carry the sample deck slots of their layout', () => {
    expectSampleSlots(deckPage.slides, 'deck-page.json');
  });

  it('SAMPLE_SLOTS has the first sample deck slots of each layout the sample has', () => {
    const layouts = [...new Set(sampleDeck.slides.map((slide) => slide.layout))].sort();
    expect(Object.keys(SAMPLE_SLOTS).sort()).toEqual(layouts);
    for (const layout of layouts) {
      const first = sampleDeck.slides.find((slide) => slide.layout === layout);
      expect(SAMPLE_SLOTS[layout], layout).toEqual(first?.slots);
    }
  });
});

describe('slot types', () => {
  // Compile-time probes: each @ts-expect-error fails the build (tsc in
  // `pnpm run build`) if the slot types stop rejecting that mistake.
  it('reject misspelt, missing and out-of-range slots in typed code', () => {
    const misspelt: PracticeSlots = {
      headline: 'h',
      // @ts-expect-error `leed` is not a field of a do item
      do: [{ leed: 'l', text: 't' }],
      comes_back: [],
      avoid: [],
    };
    // @ts-expect-error `avoid` is required
    const missing: PracticeSlots = { headline: 'h', do: [], comes_back: [] };
    // @ts-expect-error warn is a table cell tone, not a panel tone
    const tone: CalloutBlock = { tone: 'warn', text: 't' };
    // @ts-expect-error a gantt row needs a status
    const row: GanttRow = { label: 'l', start: 1, end: 2 };
    expect([misspelt, missing, tone, row]).toHaveLength(4);
  });
});

describe('layouts', () => {
  it('the registry has exactly one component per layout name', () => {
    expect(Object.keys(LAYOUTS).sort()).toEqual([...LAYOUT_NAMES].sort());
  });

  it('the layout slot fixtures set every slot key of every layout', () => {
    for (const layout of LAYOUT_NAMES)
      expect(sorted(LAYOUT_SLOTS[layout]), layout).toEqual([...SLOT_KEYS[layout]].sort());
  });
});

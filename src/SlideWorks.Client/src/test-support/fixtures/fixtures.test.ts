import { describe, expect, it } from 'vitest';
import type {
  ActivityRow,
  AdminUserRow,
  AdminUsersView,
  DeckPageSlide,
  DeckPageView,
  DeckPlaySlide,
  DeckPlayView,
  DeckRow,
  DecksLibraryView,
  LastChange,
  MeView,
  SharedDeckRow,
  SharingChange,
  SharingView,
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
  deckPage,
  deckPlay,
  decksLibrary,
  me,
  sampleDeck,
  sharing,
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
});
const LIBRARY = keys<DecksLibraryView>({ decks: true, sharedWithMe: true, connect: true });
const DECK_PAGE = keys<DeckPageView>({
  id: true,
  slug: true,
  title: true,
  author: true,
  isOwner: true,
  playUrl: true,
  sharing: true,
  parts: true,
  slides: true,
  activity: true,
});
const DECK_PAGE_SLIDE = keys<DeckPageSlide>({
  number: true,
  layout: true,
  part: true,
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
});
const DECK_PLAY = keys<DeckPlayView>({
  id: true,
  slug: true,
  title: true,
  author: true,
  parts: true,
  slides: true,
});
const DECK_PLAY_SLIDE = keys<DeckPlaySlide>({
  number: true,
  layout: true,
  part: true,
  steps: true,
  slots: true,
});
const SHARING = keys<SharingView>({ level: true, viewers: true, link: true, lastChange: true });
const SHARING_CHANGE = keys<SharingChange>({ actor: true, actorKind: true, atLabel: true });

const sorted = (value: object) => Object.keys(value).sort();
const ACTOR_KINDS = ['agent', 'person'];
const VIAS = ['mcp', 'web'];
const LEVELS = ['private', 'link', 'named'];
const DECK_ID = /^[0-9A-Za-z]{12}$/;

function nullPaths(value: unknown, path = '$'): string[] {
  if (value === null) return [path];
  if (Array.isArray(value)) return value.flatMap((item, i) => nullPaths(item, `${path}[${i}]`));
  if (typeof value === 'object')
    return Object.entries(value).flatMap(([key, item]) => nullPaths(item, `${path}.${key}`));
  return [];
}

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

function expectDeckRow(row: DeckRow, expected: string[]) {
  expect(sorted(row)).toEqual(expected);
  expect(row.id).toMatch(DECK_ID);
  expect(LEVELS).toContain(row.sharing);
  expectLastChange(row.lastChange);
}

describe('screen JSON examples', () => {
  it('give every nullable field a non-null value', () => {
    for (const screen of [me, adminUsers, decksLibrary, deckPage, deckPlay, sharing])
      expect(nullPaths(screen)).toEqual([]);
  });

  it('me.json matches MeView', () => {
    expect(sorted(me)).toEqual(ME);
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
    for (const slide of deckPlay.slides) {
      expect(sorted(slide)).toEqual(DECK_PLAY_SLIDE);
      expectSlots(slide.layout, slide.slots);
    }
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
    // @ts-expect-error a callout's tone is good, bad or info; warn is a table cell tone
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

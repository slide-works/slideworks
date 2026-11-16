import { describe, expect, it } from 'vitest';
import type {
  ActivityRow,
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
import { LAYOUT_NAMES, SLOT_KEYS, type LayoutName } from '../../slides/slots';
import { LAYOUT_SLOTS } from './layoutSlots';
import { deckPage, deckPlay, decksLibrary, me, sharing } from './screens';

// The run-time half of the fixture check: `satisfies` in screens.ts checks the
// shape, these check the exact key sets and the literal values. Each table is a
// Record over the type's keys, so it can't miss or invent a key either.
const keys = <T>(table: Record<keyof T, true>) => Object.keys(table).sort();
const ME = keys<MeView>({ email: true, name: true, pictureUrl: true });
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
    for (const screen of [me, decksLibrary, deckPage, deckPlay, sharing])
      expect(nullPaths(screen)).toEqual([]);
  });

  it('me.json matches MeView', () => {
    expect(sorted(me)).toEqual(ME);
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

describe('layouts', () => {
  it('the registry has exactly one component per layout name', () => {
    expect(Object.keys(LAYOUTS).sort()).toEqual([...LAYOUT_NAMES].sort());
  });

  it('the layout slot fixtures set every slot key of every layout', () => {
    for (const layout of LAYOUT_NAMES)
      expect(sorted(LAYOUT_SLOTS[layout]), layout).toEqual([...SLOT_KEYS[layout]].sort());
  });
});

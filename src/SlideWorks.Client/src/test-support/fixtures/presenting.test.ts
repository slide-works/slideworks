import { describe, expect, it } from 'vitest';
import type {
  LivePresentation,
  LivePresentationsView,
  LiveWindow,
  PresenterSlide,
  PresenterView,
} from '../../api/types';
import type {
  PresentationConnection,
  PresentationState,
  PresentationTimer,
} from '../../presentation/types';
import { livePresentations, presentationState, presenterView } from './presenting';
import { sampleDeck } from './screens';

// The run-time half of the presenting fixtures' check: `satisfies` in
// presenting.ts checks field names and value kinds; these check the exact key
// sets, the literal values, and that the three samples describe one talk.
const keys = <T>(table: Record<keyof T, true>) => Object.keys(table).sort();
const sorted = (value: object) => Object.keys(value).sort();

const STATE = keys<PresentationState>({
  deckId: true,
  epoch: true,
  seq: true,
  slide: true,
  step: true,
  black: true,
  skipDeepDives: true,
  timer: true,
  talkMinutes: true,
  connected: true,
});
const TIMER = keys<PresentationTimer>({ state: true, elapsedMs: true });
const CONNECTION = keys<PresentationConnection>({ clientId: true, role: true, label: true });
const PRESENTER = keys<PresenterView>({
  id: true,
  slug: true,
  title: true,
  author: true,
  parts: true,
  look: true,
  join: true,
  slides: true,
  talkMinutes: true,
  audienceUrl: true,
  remoteUrl: true,
});
const PRESENTER_SLIDE = keys<PresenterSlide>({
  number: true,
  layout: true,
  part: true,
  deepDive: true,
  surface: true,
  steps: true,
  slots: true,
  notes: true,
  headline: true,
});
const LIVE = keys<LivePresentationsView>({ presentations: true });
const LIVE_PRESENTATION = keys<LivePresentation>({
  deckId: true,
  title: true,
  slide: true,
  total: true,
  startedLabel: true,
  windows: true,
});
const LIVE_WINDOW = keys<LiveWindow>({ role: true, label: true });
const ROLES = ['presenter', 'audience', 'phone'];

describe('presentation-state.json', () => {
  it('matches PresentationState', () => {
    const state = presentationState();
    expect(sorted(state)).toEqual(STATE);
    expect(sorted(state.timer)).toEqual(TIMER);
    expect(state.timer.state).toBe('running');
    expect(state.connected.map((c) => c.role)).toEqual(['presenter', 'audience', 'phone']);
    for (const connection of state.connected) expect(sorted(connection)).toEqual(CONNECTION);
  });

  it('applies overrides to a fresh copy', () => {
    const changed = presentationState({ slide: 3, black: true });
    changed.connected.pop();
    expect(changed.slide).toBe(3);
    expect(changed.black).toBe(true);
    expect(presentationState().connected).toHaveLength(3);
  });
});

describe('presenter.json', () => {
  it('matches PresenterView', () => {
    const view = presenterView();
    expect(sorted(view)).toEqual(PRESENTER);
    for (const slide of view.slides) expect(sorted(slide)).toEqual(PRESENTER_SLIDE);
    expect(view.author).toBe('Sam Rivera');
    expect(view.talkMinutes).toBe(30);
    expect(view.audienceUrl).toBe(`https://slideworks.example/d/${view.id}/${view.slug}?present`);
    expect(view.remoteUrl).toBe(`https://slideworks.example/remote?deck=${view.id}`);
  });

  it('is the sample deck with deep dives 5, 10 and 12, and marked-up notes on 1, 8, 10 and 12', () => {
    const view = presenterView();
    expect(view.slides.map((s) => s.number)).toEqual(sampleDeck.slides.map((s) => s.number));
    expect(view.slides.map((s) => [s.layout, s.steps, s.slots])).toEqual(
      sampleDeck.slides.map((s) => [s.layout, s.steps, s.slots]),
    );
    // Screen examples carry no nulls (K4): every slide has notes, and a test
    // that needs a slide without notes sets that slide's notes to null.
    expect(view.slides.every((s) => s.notes !== null)).toBe(true);
    expect(view.slides.filter((s) => s.notes?.includes('**')).map((s) => s.number)).toEqual([
      1, 8, 10, 12,
    ]);
    expect(view.slides.filter((s) => s.deepDive).map((s) => s.number)).toEqual([5, 10, 12]);
    expect(view.slides.every((s) => s.headline !== null)).toBe(true);
    // The notes markup NotesText renders: **bold** and "- " lines.
    expect(view.slides[0].notes).toContain('**');
    expect(view.slides[0].notes).toMatch(/^- /m);
  });
});

describe('presentations.json', () => {
  it('matches LivePresentationsView', () => {
    const live = livePresentations();
    expect(sorted(live)).toEqual(LIVE);
    for (const presentation of live.presentations) {
      expect(sorted(presentation)).toEqual(LIVE_PRESENTATION);
      for (const window of presentation.windows) {
        expect(sorted(window)).toEqual(LIVE_WINDOW);
        expect(ROLES).toContain(window.role);
      }
    }
  });
});

function hasNull(value: unknown): boolean {
  if (value === null) return true;
  return typeof value === 'object' && Object.values(value).some(hasNull);
}

function nullPaths(value: unknown, path = '$'): string[] {
  if (value === null) return [path];
  if (Array.isArray(value)) return value.flatMap((item, i) => nullPaths(item, `${path}[${i}]`));
  if (typeof value === 'object')
    return Object.entries(value).flatMap(([key, item]) => nullPaths(item, `${path}.${key}`));
  return [];
}

// What a built-in template leaves null in a look (T7): the presenter sample shows a built-in deck as the server sends
// it, so these are its only nulls.
const BUILT_IN_LOOK_NULLS = [
  '$.look.template.titleArt',
  '$.look.logos.light',
  '$.look.logos.dark',
  '$.look.art.generator',
  '$.look.art.url',
  '$.look.templateVersion',
  '$.look.latestVersion',
];

describe('the presenting samples', () => {
  it('give every nullable field a non-null value', () => {
    expect(nullPaths(presenterView())).toEqual(BUILT_IN_LOOK_NULLS);
    expect(hasNull(presentationState())).toBe(false);
    expect(hasNull(livePresentations())).toBe(false);
  });

  it('describe one talk: the same deck, live on the same slide', () => {
    const view = presenterView();
    const state = presentationState();
    const [live] = livePresentations().presentations;
    expect(state.deckId).toBe(view.id);
    expect(state.talkMinutes).toBe(view.talkMinutes);
    expect(live.deckId).toBe(view.id);
    expect(live.title).toBe(view.title);
    expect(live.total).toBe(view.slides.length);
    expect(live.slide).toBe(state.slide);
  });
});

// The max-content fit fixtures shared with the server: src/contracts/fit-fixtures
// holds one file per layout, <layout>.json, each an object of named fixtures
// for that layout. SlideWorks.UnitTest's FitFixturesTests proves each one is
// accepted (or, with `accepted: false`, refused as it stands) and that one more
// word in a `grow` field is refused.
//
// Test support only: never import this from a component. The folder is read
// from disk, because a CT spec loads its imports in Node, where Vite's
// import.meta.glob does not exist. The client has no Node types, hence the two
// calls typed here.
interface Fs {
  readFileSync(path: URL, encoding: 'utf8'): string;
  readdirSync(path: URL, options: { encoding: 'utf8' }): string[];
}
const fs = (
  globalThis as unknown as { process: { getBuiltinModule(id: 'node:fs'): Fs } }
).process.getBuiltinModule('node:fs');

const folder = new URL('../../../../contracts/fit-fixtures/', import.meta.url);

export interface FitFixtureEntry {
  layout: string;
  // The layout's fit rule's height for the body, in px below the body's top.
  estimate?: number;
  // False when the fixture is past the maximum and is refused as it stands.
  accepted?: boolean;
  // JSON pointers into `slots`: one more word in any of them is refused.
  grow: string[];
  refusedBy?: 'fit' | 'length';
  surface?: 'dark';
  slots: unknown;
}

function readFolder(): Record<string, FitFixtureEntry> {
  const all: Record<string, FitFixtureEntry> = {};
  const files = fs
    .readdirSync(folder, { encoding: 'utf8' })
    .filter((file) => file.endsWith('.json'))
    .sort();
  for (const file of files) {
    const layout = file.slice(0, -'.json'.length);
    const entries = JSON.parse(fs.readFileSync(new URL(file, folder), 'utf8')) as Record<
      string,
      FitFixtureEntry
    >;
    for (const [name, fixture] of Object.entries(entries)) {
      if (fixture.layout !== layout) {
        throw new Error(`fit-fixtures/${file} holds ${name}, a fixture for ${fixture.layout}`);
      }
      if (name in all) throw new Error(`fit-fixtures holds two fixtures named ${name}`);
      all[name] = fixture;
    }
  }
  return all;
}

const ALL = readFolder();

// The names fit-fixtures.json held before it was split into one file per
// layout. The family fixture lists (CardLayouts, ContentLayouts and
// DataLayouts.fixtures.ts) keep only these, so a fixture added later never
// enters a family spec's loop; a later fixture is drawn with fitFixture or
// fitFixturesOf.
export const LEGACY_FIT_FIXTURES: readonly string[] = [
  'bulletsEight',
  'bulletsCallout',
  'twoColumnItems',
  'twoColumnText',
  'practice',
  'compare',
  'numberedCardsProse',
  'numberedCardsTitleCase',
  'numberedCardsWide',
  'rulesProse',
  'rulesTitleCase',
  'rulesWide',
  'cardsProse',
  'cardsTitleCase',
  'cardsWide',
  'tableTitle',
  'tableProse',
  'chartTitle',
  'chartProse',
  'ganttTitle',
  'ganttProse',
  'imageTitle',
  'imageProse',
  'joinKicker',
  'pollEight',
  'qaTitle',
];

// The fixture of that name, whichever layout's file holds it.
export function fitFixture(name: string): FitFixtureEntry {
  if (!Object.hasOwn(ALL, name)) throw new Error(`fit-fixtures has no fixture ${name}`);
  return ALL[name];
}

// Every fixture of a layout, by name; empty for a layout with no file.
export function fitFixturesOf(layout: string): Record<string, FitFixtureEntry> {
  return Object.fromEntries(Object.entries(ALL).filter(([, fixture]) => fixture.layout === layout));
}

// The fixtures of a family of layouts that fit-fixtures.json already held.
export function legacyFitFixturesOf(layouts: readonly string[]): Record<string, FitFixtureEntry> {
  return Object.fromEntries(
    layouts
      .flatMap((layout) => Object.entries(fitFixturesOf(layout)))
      .filter(([name]) => LEGACY_FIT_FIXTURES.includes(name)),
  );
}

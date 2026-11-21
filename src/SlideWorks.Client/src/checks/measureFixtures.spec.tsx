import { test, expect } from '@playwright/experimental-ct-react';
import names from '@contracts/layout-names.json' with { type: 'json' };
import { DATA_FIT_FIXTURES, wideTwin } from '../slides/layouts/DataLayouts.fixtures';
import { lookViewOf } from '../test-support/fixtures/lookViewOf';
import { fitFixturesOf, type FitFixtureEntry } from '../test-support/fixtures/fitFixtures';
import { sampleDeck } from '../test-support/fixtures/screens';
import { MeasureProbe } from '../test-support/MeasureProbe';
import { stubApi } from '../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import type { SlideFinding } from './types';

// The overflow half of the fixture sweep: every accepted maximum fit fixture, drawn at 1920x1080 inside the real frame
// and measured by measureSlide. No screenshots.

test.use({ viewport: { width: 1920, height: 1080 } });

// interactive: false, as the deck page's thumbnails draw them, so the live layouts (qa, poll, word cloud, open text,
// video) draw their static face.
const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
  interactive: false,
};

type Case = { name: string; entry: FitFixtureEntry; slots: unknown };

const cases: Case[] = [];
for (const layout of names.layouts) {
  for (const [name, entry] of Object.entries(fitFixturesOf(layout))) {
    if (entry.accepted === false) continue;
    cases.push({ name, entry, slots: entry.slots });
  }
}
// The wide-letter twin of every data fixture in Title Case, as DataLayouts.spec draws them.
for (const [name, fixture] of Object.entries(DATA_FIT_FIXTURES)) {
  if (!name.endsWith('Title')) continue;
  const twin = wideTwin(fixture);
  cases.push({
    name: `${name.replace(/Title$/, '')}Wide`,
    entry: { ...fixture, slots: twin.slots } as FitFixtureEntry,
    slots: twin.slots,
  });
}

function slideOf({ entry, slots }: Case): RenderableSlide {
  return {
    number: 3,
    layout: entry.layout,
    part: null,
    deepDive: false,
    surface: entry.surface ?? null,
    slots,
  } as RenderableSlide;
}

// Fixtures that overflow today. Each is a real bug, left drawn so it is reported, not skipped.
const KNOWN_OVERFLOW: Record<string, string> = {
  levelsTextWide: 'overflows (y 1049-1105, footer at 1023); reported to the conductor',
};

for (const c of cases) {
  test(`every accepted maximum fit fixture draws no overflow: ${c.name}`, async ({
    mount,
    page,
  }) => {
    // The wide twin of tableTitle ends near y 1199 (SW-130). Reported on purpose: when SW-130 is fixed this case
    // flips, and the assertion below becomes "no overflow".
    const expected = c.name === 'tableWide';
    test.fail(c.name in KNOWN_OVERFLOW, KNOWN_OVERFLOW[c.name] ?? '');
    await stubApi(page, {});
    const component = await mount(
      <MeasureProbe slide={3}>
        <SlideRenderer slide={slideOf(c)} deck={deck} total={sampleDeck.slides.length} step={0} />
      </MeasureProbe>,
    );

    await expect(component).toHaveAttribute('data-findings', /.+/);
    const found = JSON.parse((await component.getAttribute('data-findings'))!) as SlideFinding[];
    const overflowing = found.filter((f) => f.rule === 'overflow');
    if (expected) expect(overflowing.length).toBeGreaterThan(0);
    else expect(overflowing, JSON.stringify(overflowing)).toEqual([]);
  });
}

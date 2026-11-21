import { test, expect } from '@playwright/experimental-ct-react';
import { lookViewOf, PALETTES } from '../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../test-support/fixtures/screens';
import { MeasureProbe } from '../test-support/MeasureProbe';
import { stubApi } from '../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import type { SlideFinding } from './types';

// The contrast and size half of the palette sweep: every sample slide drawn at 1920x1080 in the real frame under each
// built-in palette, measured by measureSlide. No screenshots.

test.use({ viewport: { width: 1920, height: 1080 } });

const looks = [
  ...PALETTES.map((p) => ({ template: 'talk', palette: p.name })),
  // Practice is drawn by the workshop template.
  { template: 'workshop', palette: 'cobalt' },
];

// Problems the sweep finds today. Each is a real bug, left drawn so it is reported. A look's problems minus its KNOWN
// entries must be [], and every KNOWN entry must still be found, so fixing a bug flips its test and the entry is
// deleted.
type Known = { slide: number; rule: string; wherePrefix: string; bug: string };

// SW-3-new: the Journey part names draw at 18 px in the sample deck (slides 2 and 7), under the 20 px bar. Not yet filed.
const journeyNames = [2, 7].flatMap((slide) =>
  ['The cost', 'Slice it', 'Ship it', 'Make it stick'].map((name) => ({
    slide,
    rule: 'smallText',
    wherePrefix: `text "${name}"`,
    bug: 'SW-3-new',
  })),
);
// SW-666: the gantt ship marker and the Practice "What comes back" heading, both 3.1:1 under cobalt.
const cobalt: Known[] = [
  { slide: 5, rule: 'lowContrast', wherePrefix: 'text "▲"', bug: 'SW-666' },
  { slide: 8, rule: 'lowContrast', wherePrefix: 'text "What comes back"', bug: 'SW-666' },
];
const KNOWN: Record<string, Known[]> = {
  'talk cobalt': [...journeyNames, ...cobalt],
  'talk ember': journeyNames,
  'talk plum': journeyNames,
  // SW-665: the table cells inherit the page ink, which is dark on midnight's dark card (1.0:1, every body cell).
  'talk midnight': [
    ...journeyNames,
    { slide: 11, rule: 'lowContrast', wherePrefix: 'text "', bug: 'SW-665' },
  ],
  'workshop cobalt': [...journeyNames, ...cobalt],
};

for (const { template, palette } of looks) {
  test(`the sample deck has no contrast or size warnings under every built-in palette: ${template} ${palette}`, async ({
    mount,
    page,
  }) => {
    await stubApi(page, {});
    const deck: SlideDeck = {
      title: sampleDeck.title,
      author: sampleDeck.author,
      parts: sampleDeck.parts,
      look: lookViewOf(template, palette),
      deepDives: [],
      interactive: false,
    };
    const problems: (Known & { message: string })[] = [];
    for (const slide of sampleDeck.slides) {
      const component = await mount(
        <MeasureProbe slide={slide.number} look={deck.look}>
          <SlideRenderer
            slide={slide as RenderableSlide}
            deck={deck}
            total={sampleDeck.slides.length}
            step={0}
          />
        </MeasureProbe>,
      );
      await expect(component).toHaveAttribute('data-findings', /.+/);
      const found = JSON.parse((await component.getAttribute('data-findings'))!) as SlideFinding[];
      for (const f of found) {
        if (f.rule === 'lowContrast' || f.rule === 'smallText') {
          problems.push({
            slide: slide.number,
            rule: f.rule,
            wherePrefix: f.where ?? '',
            bug: '',
            message: f.message,
          });
        }
      }
      await component.unmount();
    }
    const known = KNOWN[`${template} ${palette}`] ?? [];
    const matches = (p: Known, k: Known) =>
      p.slide === k.slide && p.rule === k.rule && p.wherePrefix.startsWith(k.wherePrefix);
    const unknown = problems.filter((p) => !known.some((k) => matches(p, k)));
    expect(
      unknown.map((p) => `slide ${p.slide} ${p.rule} ${p.wherePrefix}: ${p.message}`),
      'new problems',
    ).toEqual([]);
    const gone = known.filter((k) => !problems.some((p) => matches(p, k)));
    expect(
      gone.map((k) => `${k.bug}: slide ${k.slide} ${k.rule} ${k.wherePrefix}`),
      'known problems no longer found: delete the entry',
    ).toEqual([]);
  });
}

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

// Built-in palettes that draw low-contrast text today. Each is a real finding, left drawn so it is reported.
const KNOWN_LOW_CONTRAST: Record<string, string> = {
  'talk cobalt':
    'gantt ship marker and practice "What comes back" heading at 3.1:1; reported to the conductor',
  'workshop cobalt':
    'gantt ship marker and practice "What comes back" heading at 3.1:1; reported to the conductor',
  'talk midnight':
    'table body text is dark on a dark table (about 1.1:1); reported to the conductor',
};

for (const { template, palette } of looks) {
  test(`the sample deck has no contrast or size warnings under every built-in palette: ${template} ${palette}`, async ({
    mount,
    page,
  }) => {
    test.fail(
      `${template} ${palette}` in KNOWN_LOW_CONTRAST,
      KNOWN_LOW_CONTRAST[`${template} ${palette}`] ?? '',
    );
    await stubApi(page, {});
    const deck: SlideDeck = {
      title: sampleDeck.title,
      author: sampleDeck.author,
      parts: sampleDeck.parts,
      look: lookViewOf(template, palette),
      deepDives: [],
      interactive: false,
    };
    const problems: string[] = [];
    for (const slide of sampleDeck.slides) {
      const component = await mount(
        <MeasureProbe slide={slide.number}>
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
          problems.push(`slide ${slide.number} (${slide.layout}) ${f.rule}: ${f.message}`);
        }
      }
      await component.unmount();
    }
    expect(problems, problems.join('\n')).toEqual([]);
  });
}

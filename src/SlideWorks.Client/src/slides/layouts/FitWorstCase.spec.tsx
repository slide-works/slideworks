import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import names from '@contracts/layout-names.json' with { type: 'json' };
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { fitFixturesOf, type FitFixtureEntry } from '../../test-support/fixtures/fitFixtures';
import { sampleDeck } from '../../test-support/fixtures/screens';
import type { Box } from '../../test-support/measure/box';
import { drawnBox } from '../../test-support/measure/drawnBox';
import { paintedExtent } from '../../test-support/measure/paintedExtent';
import { capitals } from '../../test-support/text/maxText';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

// The worst legal text, drawn region by region. Every accepted fit fixture
// (src/contracts/fit-fixtures/<layout>.json) is drawn as it stands, then once
// per region with that region's text swapped for a twin of the same length,
// while every other region stays at the fixture other specs already prove:
// - allW: unbroken W (no spaces), the widest text a font draws and the true
//   worst case for overflow-wrap:anywhere;
// - capitals: prose in capitals.
// The server counts characters, not glyph widths, so a twin of the same length
// is as legal as the fixture. What the layout draws must stay inside the
// content box, x 120-1800 and y 150-996, in layout px.

test.use({ viewport: { width: 1920, height: 1080 } });

const CONTENT: Box = { left: 120, top: 150, right: 1800, bottom: 996 };

// interactive: false, as measureFixtures.spec draws them, so the live layouts
// (qa, poll, word cloud, open text, video) draw their static face.
const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
  interactive: false,
};

// Slot fields that hold a value the layout reads, not text it draws (an enum,
// an id, a url, an icon name, alt text). A twin leaves them as they are.
const NOT_TEXT = new Set([
  'alt',
  'count',
  'fill',
  'frame',
  'icon',
  'image',
  'kind',
  'position',
  'reveal',
  'side',
  'state',
  'status',
  'tone',
  'url',
]);

// Unbroken W at `length` characters, keeping any line breaks the text has.
// maxText's allW breaks into words of five, so it is not the worst case for
// overflow-wrap:anywhere.
const unbrokenW = (text: string) => text.replace(/[^\n]/g, 'W');
const capitalsOf = (text: string) =>
  text
    .split('\n')
    .map((line) => capitals(line.length))
    .join('\n');

const VARIANTS = { allW: unbrokenW, capitals: capitalsOf } as const;
type Variant = keyof typeof VARIANTS;

// `value` with every drawn string swapped by `twin`; null when it holds none.
function twinOf(value: unknown, key: string, twin: (text: string) => string): unknown {
  if (NOT_TEXT.has(key)) return value;
  if (typeof value === 'string') return twin(value);
  if (Array.isArray(value)) return value.map((item) => twinOf(item, key, twin));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).map(([field, item]) => [field, twinOf(item, field, twin)]),
    );
  }
  return value;
}

// Fixture cases that overflow today: `<fixture> <region> <variant>` (region
// `fixture` is the fixture as it stands) to the open Bug that tracks it. Each
// is asserted to still overflow, so a fix turns this spec red until the entry
// is removed. One Bug per layout covers both variants; the levels and table
// cases are the open SW-664 and SW-130.
const KNOWN_OVERFLOWS: Record<string, string> = {
  'barsNote bars allW': 'SW-706',
  'barsNote bars capitals': 'SW-706',
  'barsNote unit allW': 'SW-706',
  'barsSix bars allW': 'SW-706',
  'barsSix bars capitals': 'SW-706',
  'bulletsCallout callout allW': 'SW-707',
  'bulletsCallout headline allW': 'SW-707',
  'bulletsCallout headline capitals': 'SW-707',
  'bulletsCallout items allW': 'SW-707',
  'bulletsCallout items capitals': 'SW-707',
  'bulletsEight headline allW': 'SW-707',
  'bulletsEight headline capitals': 'SW-707',
  'bulletsEight items allW': 'SW-707',
  'bulletsEight items capitals': 'SW-707',
  'bulletsIcons headline allW': 'SW-707',
  'bulletsIcons headline capitals': 'SW-707',
  'bulletsIcons items allW': 'SW-707',
  'bulletsIcons items capitals': 'SW-707',
  'cardsCallout cards allW': 'SW-708',
  'cardsGridLarge cards allW': 'SW-708',
  'cardsGridLarge cards capitals': 'SW-708',
  'cardsGridLarge headline allW': 'SW-708',
  'cardsGridMax cards allW': 'SW-708',
  'cardsGridMax cards capitals': 'SW-708',
  'cardsGridMax headline allW': 'SW-708',
  'cardsIcons cards allW': 'SW-708',
  'cardsIconsWide cards allW': 'SW-708',
  'cardsIconsWide headline allW': 'SW-708',
  'cardsProse cards allW': 'SW-708',
  'cardsProse headline allW': 'SW-708',
  'cardsRowMax cards allW': 'SW-708',
  'cardsRowMax headline allW': 'SW-708',
  'cardsTitleCase cards allW': 'SW-708',
  'cardsTitleCase headline allW': 'SW-708',
  'cardsWide cards allW': 'SW-708',
  'cardsWide headline allW': 'SW-708',
  'cardsWide headline capitals': 'SW-708',
  'chartCallout caption allW': 'SW-709',
  'chartCalloutWide caption allW': 'SW-709',
  'chartCalloutWide headline allW': 'SW-709',
  'chartProse caption allW': 'SW-709',
  'chartTitle caption allW': 'SW-709',
  'compare failure allW': 'SW-710',
  'compare failure capitals': 'SW-710',
  'compare headline allW': 'SW-710',
  'compare headline capitals': 'SW-710',
  'compare success allW': 'SW-710',
  'compare success capitals': 'SW-710',
  'compareCallout failure allW': 'SW-710',
  'compareCallout headline allW': 'SW-710',
  'compareCallout success allW': 'SW-710',
  'compareLineMax failure allW': 'SW-710',
  'compareLineMax failure capitals': 'SW-710',
  'compareLineMax headline allW': 'SW-710',
  'compareLineMax headline capitals': 'SW-710',
  'compareLineMax success allW': 'SW-710',
  'compareLineMax success capitals': 'SW-710',
  'comparePointsStampFooter failure allW': 'SW-710',
  'comparePointsStampFooter failure capitals': 'SW-710',
  'comparePointsStampFooter headline allW': 'SW-710',
  'comparePointsStampFooter headline capitals': 'SW-710',
  'comparePointsStampFooter success allW': 'SW-710',
  'comparePointsStampFooter success capitals': 'SW-710',
  'compareRowsMax failure allW': 'SW-710',
  'compareRowsMax failure capitals': 'SW-710',
  'compareRowsMax headline allW': 'SW-710',
  'compareRowsMax headline capitals': 'SW-710',
  'compareRowsMax success allW': 'SW-710',
  'compareRowsMax success capitals': 'SW-710',
  'ganttProse headline allW': 'SW-711',
  'ganttProse headline capitals': 'SW-711',
  'ganttTitle headline allW': 'SW-711',
  'ganttTitle headline capitals': 'SW-711',
  'imageBelowLeadPoints headline allW': 'SW-712',
  'imageBelowLeadPoints points allW': 'SW-712',
  'imageBelowLeadPoints points capitals': 'SW-712',
  'imageCallout points allW': 'SW-712',
  'imageProse points allW': 'SW-712',
  'imageRightMax callout allW': 'SW-712',
  'imageRightMax headline allW': 'SW-712',
  'imageRightMax headline capitals': 'SW-712',
  'imageRightMax points allW': 'SW-712',
  'imageRightMaxWide callout allW': 'SW-712',
  'imageRightMaxWide callout capitals': 'SW-712',
  'imageRightMaxWide caption allW': 'SW-712',
  'imageRightMaxWide caption capitals': 'SW-712',
  'imageRightMaxWide fixture': 'SW-712',
  'imageRightMaxWide headline allW': 'SW-712',
  'imageRightMaxWide headline capitals': 'SW-712',
  'imageRightMaxWide kicker allW': 'SW-712',
  'imageRightMaxWide kicker capitals': 'SW-712',
  'imageRightMaxWide points allW': 'SW-712',
  'imageRightMaxWide points capitals': 'SW-712',
  'imageTitle points allW': 'SW-712',
  'joinKicker kicker allW': 'SW-713',
  'levelsSeven levels allW': 'SW-664',
  'levelsSeven main allW': 'SW-664',
  'levelsText headline allW': 'SW-664',
  'levelsText headline capitals': 'SW-664',
  'levelsText levels allW': 'SW-664',
  'levelsText levels capitals': 'SW-664',
  'levelsTextWide callout allW': 'SW-664',
  'levelsTextWide callout capitals': 'SW-664',
  'levelsTextWide fixture': 'SW-664',
  'levelsTextWide headline allW': 'SW-664',
  'levelsTextWide headline capitals': 'SW-664',
  'levelsTextWide kicker allW': 'SW-664',
  'levelsTextWide kicker capitals': 'SW-664',
  'levelsTextWide levels allW': 'SW-664',
  'levelsTextWide levels capitals': 'SW-664',
  'levelsTextWide main allW': 'SW-664',
  'levelsTextWide main capitals': 'SW-664',
  'linksSixQr headline allW': 'SW-714',
  'linksSixQr headline capitals': 'SW-714',
  'linksSixQr links allW': 'SW-714',
  'linksSixQr links capitals': 'SW-714',
  'linksWrapped headline allW': 'SW-714',
  'linksWrapped headline capitals': 'SW-714',
  'linksWrapped links allW': 'SW-714',
  'linksWrapped links capitals': 'SW-714',
  'numberedCardsFourMax cards allW': 'SW-715',
  'numberedCardsProse cards allW': 'SW-715',
  'numberedCardsTitleCase cards allW': 'SW-715',
  'numberedCardsWide cards allW': 'SW-715',
  'numberedCardsWide headline allW': 'SW-715',
  'pollEight options allW': 'SW-716',
  'practice avoid allW': 'SW-717',
  'practice avoid capitals': 'SW-717',
  'practice callout allW': 'SW-717',
  'practice callout capitals': 'SW-717',
  'practice comes_back allW': 'SW-717',
  'practice comes_back capitals': 'SW-717',
  'practice do allW': 'SW-717',
  'practice do capitals': 'SW-717',
  'practice headline allW': 'SW-717',
  'practice headline capitals': 'SW-717',
  'quoteFull follow_up allW': 'SW-718',
  'quoteFull follow_up capitals': 'SW-718',
  'quoteFull headline allW': 'SW-718',
  'quoteFull headline capitals': 'SW-718',
  'quoteFull quote allW': 'SW-718',
  'quoteFull quote capitals': 'SW-718',
  'quoteFull source allW': 'SW-718',
  'quoteFull stat allW': 'SW-718',
  'quoteStatOnly stat allW': 'SW-718',
  'rulesCallout rules allW': 'SW-719',
  'rulesProse headline allW': 'SW-719',
  'rulesProse headline capitals': 'SW-719',
  'rulesProse rules allW': 'SW-719',
  'rulesProse rules capitals': 'SW-719',
  'rulesTitleCase headline allW': 'SW-719',
  'rulesTitleCase headline capitals': 'SW-719',
  'rulesTitleCase rules allW': 'SW-719',
  'rulesTitleCase rules capitals': 'SW-719',
  'stackSeven points allW': 'SW-720',
  'stackSeven segments allW': 'SW-720',
  'stackSeven segments capitals': 'SW-720',
  'statementMax statement allW': 'SW-721',
  'statementMaxWide statement allW': 'SW-721',
  'tableCallout rows allW': 'SW-130',
  'tableCallout rows capitals': 'SW-130',
  'tableCells80 rows allW': 'SW-130',
  'tableProse columns allW': 'SW-130',
  'tableProse headline allW': 'SW-130',
  'tableProse headline capitals': 'SW-130',
  'tableProse rows allW': 'SW-130',
  'tableProse rows capitals': 'SW-130',
  'tableTitle columns allW': 'SW-130',
  'tableTitle headline allW': 'SW-130',
  'tableTitle headline capitals': 'SW-130',
  'tableTitle rows allW': 'SW-130',
  'tableTitle rows capitals': 'SW-130',
  'twoColumnCallout left allW': 'SW-722',
  'twoColumnItems headline allW': 'SW-722',
  'twoColumnItems headline capitals': 'SW-722',
  'twoColumnItems left allW': 'SW-722',
  'twoColumnItems left capitals': 'SW-722',
  'twoColumnText left allW': 'SW-722',
  'twoColumnText right allW': 'SW-722',
};

function slideOf(entry: FitFixtureEntry, slots: unknown): RenderableSlide {
  return {
    number: 3,
    layout: entry.layout,
    part: null,
    deepDive: false,
    surface: entry.surface ?? null,
    slots,
  } as RenderableSlide;
}

// What the layout draws, in layout px: the frame's used width over its drawn width undoes any
// transform a player or thumbnail puts on it.
async function drawnInLayoutPx(page: Page, layout: string): Promise<Box> {
  const scale = await page
    .getByTestId('slide-frame')
    .evaluate((frame: HTMLElement) => frame.offsetWidth / frame.getBoundingClientRect().width);
  // paintedExtent where the layout has a body (a rules numeral ends at its
  // baseline, not its font box); drawnBox for the bodiless ones (divider,
  // statement).
  const testId = `layout-${layout}`;
  const hasBody = (await page.getByTestId(testId).locator('.sw-body').count()) > 0;
  // The top is the element boxes' top: a line's glyphs rise a little above
  // its line box, as in ContentLayouts.spec.tsx.
  const box = hasBody
    ? await paintedExtent(page, testId, {
        images: true,
        baselineText: '[data-testid="rules-number"]',
      }).then(({ drawn, boxTop }) => ({ ...drawn, top: boxTop }))
    : await drawnBox(page, testId);
  return {
    left: box.left * scale,
    top: box.top * scale,
    right: box.right * scale,
    bottom: box.bottom * scale,
  };
}

const inside = (box: Box) =>
  box.left >= CONTENT.left &&
  box.top >= CONTENT.top &&
  box.right <= CONTENT.right + 0.5 &&
  box.bottom <= CONTENT.bottom + 0.5;

const round = (box: Box) =>
  `x ${Math.round(box.left)}-${Math.round(box.right)}, y ${Math.round(box.top)}-${Math.round(box.bottom)}`;

for (const layout of names.layouts) {
  for (const [name, entry] of Object.entries(fitFixturesOf(layout))) {
    if (entry.accepted === false) continue;
    test(`worst legal text stays inside the content box: ${name}`, async ({ mount, page }) => {
      await stubApi(page, {});
      const slots = entry.slots as Record<string, unknown>;
      const cases: [string, unknown][] = [['fixture', slots]];
      for (const region of Object.keys(slots)) {
        for (const variant of Object.keys(VARIANTS) as Variant[]) {
          const twin = twinOf(slots[region], region, VARIANTS[variant]);
          if (JSON.stringify(twin) === JSON.stringify(slots[region])) continue;
          cases.push([`${region} ${variant}`, { ...slots, [region]: twin }]);
        }
      }

      const overflowing: string[] = [];
      for (const [label, caseSlots] of cases) {
        const component = await mount(
          <SlideRenderer
            slide={slideOf(entry, caseSlots)}
            deck={deck}
            total={sampleDeck.slides.length}
            step={99}
          />,
        );
        const drawn = await drawnInLayoutPx(page, layout);
        console.log(`${name} ${label}: ${round(drawn)}`);
        if (!inside(drawn)) overflowing.push(`${name} ${label}`);
        await component.unmount();
      }

      const known = Object.keys(KNOWN_OVERFLOWS).filter((key) => key.startsWith(`${name} `));
      expect(
        overflowing.sort(),
        'overflowing cases (KNOWN_OVERFLOWS lists the tracked ones)',
      ).toEqual(known.sort());
    });
  }
}

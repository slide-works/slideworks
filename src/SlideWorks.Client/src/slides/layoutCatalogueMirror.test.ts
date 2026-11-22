// The client's literal copies of the layout catalogue, checked against the
// generated src/contracts/layout-catalogue.json (written from LayoutCatalogue by
// LayoutCatalogueJsonTests). The TS lists stay literal so the types stay unions;
// these checks fail when a layout or slot is added to LayoutCatalogue and a copy
// is not, and each message names every file to touch.
import catalogue from '@contracts/layout-catalogue.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { LAYOUT_SLOTS, REQUIRED_SLOT_KEYS } from '../test-support/fixtures/layoutSlots';
import {
  LAYOUT_NAMES,
  LAYOUT_SURFACE,
  NO_TRACKER_LAYOUTS,
  SLOT_KEYS,
  type LayoutName,
} from './slots';

const NEW_LAYOUT_FILES = [
  'src/SlideWorks.Client/src/slides/slots.ts (LAYOUT_NAMES, LAYOUT_SURFACE, Slots, SLOT_KEYS)',
  'src/SlideWorks.Client/src/slides/slots/<layout>.ts (the slot type and its KEYS)',
  'src/SlideWorks.Client/src/slides/layouts/index.ts (LAYOUTS) and layouts/<Name>Layout.tsx',
  'src/SlideWorks.Client/src/test-support/fixtures/layoutSlots.ts (LAYOUT_SLOTS, REQUIRED_SLOT_KEYS)',
  'src/SlideWorks.Client/src/test-support/fixtures/layoutSlots/<layout>.ts (SLOTS, REQUIRED)',
  'src/SlideWorks.Client/src/look/deepDivesOf.ts (a case when the layout has no headline slot)',
  'src/contracts/layout-names.json',
].join('; ');

const SLOT_FILES =
  'src/SlideWorks.Client/src/slides/slots/<layout>.ts (the slot type and its KEYS) and ' +
  'src/SlideWorks.Client/src/test-support/fixtures/layoutSlots/<layout>.ts (SLOTS, REQUIRED)';

const sorted = (values: readonly string[]) => [...values].sort();

const layoutOf = (name: string) => {
  const layout = catalogue.layouts.find((l) => l.name === name);
  if (!layout)
    throw new Error(`${name} is not in layout-catalogue.json; touch ${NEW_LAYOUT_FILES}`);
  return layout;
};

describe('the client mirrors of layout-catalogue.json', () => {
  it('LAYOUT_NAMES lists the catalogue layouts in order', () => {
    expect(
      [...LAYOUT_NAMES],
      `a layout changed in LayoutCatalogue; touch ${NEW_LAYOUT_FILES}`,
    ).toEqual(catalogue.layouts.map((l) => l.name));
  });

  it('LAYOUT_SURFACE matches each layout surface', () => {
    const surfaces = Object.fromEntries(LAYOUT_NAMES.map((name) => [name, LAYOUT_SURFACE[name]]));
    expect(surfaces, 'touch LAYOUT_SURFACE in src/SlideWorks.Client/src/slides/slots.ts').toEqual(
      Object.fromEntries(catalogue.layouts.map((l) => [l.name, l.surface])),
    );
  });

  it('NO_TRACKER_LAYOUTS lists the layouts without a tracker', () => {
    expect(
      sorted(NO_TRACKER_LAYOUTS),
      'touch NO_TRACKER_LAYOUTS in src/SlideWorks.Client/src/slides/slots.ts',
    ).toEqual(sorted(catalogue.layouts.filter((l) => !l.tracker).map((l) => l.name)));
  });

  it('SLOT_KEYS lists each layout slots', () => {
    for (const name of LAYOUT_NAMES) {
      const layout = layoutOf(name);
      expect(
        sorted(SLOT_KEYS[name as LayoutName]),
        `${name}: a slot changed; touch ${SLOT_FILES}`,
      ).toEqual(sorted(layout.slots.map((s) => s.name)));
    }
  });

  it('REQUIRED_SLOT_KEYS lists each layout required slots', () => {
    for (const name of LAYOUT_NAMES) {
      const layout = layoutOf(name);
      expect(
        sorted(REQUIRED_SLOT_KEYS[name as LayoutName]),
        `${name}: a required slot changed; touch ${SLOT_FILES}`,
      ).toEqual(sorted(layout.slots.filter((s) => s.required).map((s) => s.name)));
    }
  });

  // The client holds no kind map; the only runtime kind data is the value shape
  // of each slot in LAYOUT_SLOTS (every optional slot is set). Covered: text,
  // stamp, link, youtube (string), emphasis (list of object), callout (object), whole number, image,
  // object, list of text, list of object, list of text or object, list of list
  // of text or object, and every 'a | b' enum (value must be one of the options).
  // Not covered, no client runtime data to tell them apart: the string kinds are
  // not told apart from each other, and chip, levels and facts shapes.
  const isObject = (v: unknown) => typeof v === 'object' && v !== null && !Array.isArray(v);
  const isList = (v: unknown, each: (item: unknown) => boolean) =>
    Array.isArray(v) && v.length > 0 && v.every(each);
  const isString = (v: unknown) => typeof v === 'string';
  const SHAPES: Record<string, (v: unknown) => boolean> = {
    text: isString,
    emphasis: (v) => isList(v, isObject),
    callout: isObject,
    stamp: isString,
    link: isString,
    youtube: isString,
    'whole number': Number.isInteger,
    image: (v) => isObject(v) && isString((v as { image?: unknown }).image),
    object: isObject,
    'list of text': (v) => isList(v, isString),
    'list of object': (v) => isList(v, isObject),
    'list of text or object': (v) => isList(v, (i) => isString(i) || isObject(i)),
    'list of list of text or object': (v) =>
      isList(v, (row) => isList(row, (i) => isString(i) || isObject(i))),
  };

  it('LAYOUT_SLOTS fixtures hold each slot in the shape of its kind', () => {
    for (const name of LAYOUT_NAMES) {
      const fixture = LAYOUT_SLOTS[name as LayoutName] as Record<string, unknown>;
      for (const slot of layoutOf(name).slots) {
        const value = fixture[slot.name];
        if (value === undefined) continue;
        const options = slot.kind.includes(' | ') ? slot.kind.split(' | ') : null;
        const shape = SHAPES[slot.kind];
        if (!options && !shape) continue;
        const ok = options ? options.includes(value as string) : shape(value);
        expect(
          ok,
          `${name}.${slot.name}: layout-catalogue.json says kind "${slot.kind}" but the client fixture holds ${JSON.stringify(value)}; touch ${SLOT_FILES}`,
        ).toBe(true);
      }
    }
  });
});

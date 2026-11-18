// Looks for component specs (SW-2 L10), built from the built-in catalogues in
// src/contracts/templates.json and palettes.json, never from colours spelt out
// in a spec. Every spec that needs a look uses lookViewOf.
import palettesJson from '@contracts/palettes.json' with { type: 'json' };
import templatesJson from '@contracts/templates.json' with { type: 'json' };
import type {
  LookView,
  LooksPalette,
  LooksTemplate,
  PaletteTokens,
  TemplateChrome,
  TemplateShape,
  TemplateType,
} from '../../api/types';
import { lookOf } from '../../look/lookOf';
import type { LayoutName } from '../../slides/slots';
import type { Widen } from './widen';

// One entry of templates.json: the catalogue's own shape, which GET /api/looks
// narrows to a LooksTemplate.
export type CatalogueTemplate = {
  name: string;
  label: string;
  purpose: string;
  isDefault: boolean;
  layouts: LayoutName[];
  chrome: TemplateChrome;
  outline: string[];
  houseStyle: string[];
  type: TemplateType;
  shape: TemplateShape;
  titleArt: string | null;
  ornament: LooksTemplate['ornament'];
  logo: string | null;
  palette: string | null;
  lockPalette: boolean;
};

// One entry of palettes.json.
export type CataloguePalette = {
  name: string;
  label: string;
  isDefault: boolean;
  words: string[];
  swatch: string[];
  tokens: PaletteTokens;
};

export const TEMPLATES = templatesJson.templates satisfies Widen<
  CatalogueTemplate[]
> as CatalogueTemplate[];
export const PALETTES = palettesJson.palettes satisfies Widen<
  CataloguePalette[]
> as CataloguePalette[];

// The footer date label every fixture look carries (the server formats it).
export const FOOTER_DATE_LABEL = '16 Nov 2026';

// No built-in template locks its palette, so this one does: the talk template
// renamed, locked to Ember.
export const lockingTemplate: CatalogueTemplate = {
  ...TEMPLATES[0],
  name: 'locked-talk',
  label: 'Locked talk',
  isDefault: false,
  palette: 'ember',
  lockPalette: true,
};

export function templateNamed(name: string): CatalogueTemplate {
  const template =
    name === lockingTemplate.name ? lockingTemplate : TEMPLATES.find((t) => t.name === name);
  if (!template) throw new Error(`No template '${name}' in templates.json`);
  return template;
}

export function paletteNamed(name: string): CataloguePalette {
  const palette = PALETTES.find((p) => p.name === name);
  if (!palette) throw new Error(`No palette '${name}' in palettes.json`);
  return palette;
}

// A catalogue template as GET /api/looks lists it.
export function looksTemplateOf(template: CatalogueTemplate): LooksTemplate {
  return {
    name: template.name,
    label: template.label,
    purpose: template.purpose,
    isDefault: template.isDefault,
    layouts: template.layouts,
    chrome: template.chrome,
    outline: template.outline,
    type: template.type,
    shape: template.shape,
    ornament: template.ornament,
    titleArt: template.titleArt,
    lockedPalette: template.lockPalette ? template.palette : null,
    ref: template.name,
    version: null,
    builtIn: true,
    owner: null,
  };
}

// A catalogue palette as GET /api/looks lists it, with a passing contrast summary.
export function looksPaletteOf(palette: CataloguePalette): LooksPalette {
  return {
    name: palette.name,
    label: palette.label,
    isDefault: palette.isDefault,
    swatch: palette.swatch,
    tokens: palette.tokens,
    contrast: { pass: true, lowest: { pair: 'headline-accent', ratio: 4.5, bar: 3 } },
  };
}

// The look of a deck on this template (a name, or an entry such as
// lockingTemplate) and palette, with the template's own chrome and draft
// setting, no logo, a built-in's values for everything a template can bring
// (no fonts, logos, top bar, extras or custom layouts), and any field overridden.
export function lookViewOf(
  template: string | CatalogueTemplate,
  palette: string,
  overrides: Partial<LookView> = {},
): LookView {
  const entry = typeof template === 'string' ? templateNamed(template) : template;
  return {
    ...lookOf(looksTemplateOf(entry), paletteNamed(palette), FOOTER_DATE_LABEL),
    ...overrides,
  };
}

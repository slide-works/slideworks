// What a deck's template brings to its look beyond the built-in chrome (SW-50 T7):
// the records LookView carries for an uploaded template. A built-in sends every
// list empty, no logos, no top bar and no versions.
import type { PaletteTokenKey } from './types';

export type LookFontRole = 'head' | 'body' | 'mono';

// One font file of the template. url is root-relative (/api/templates/{ref}/assets/{id}).
export type LookFont = {
  family: string;
  role: LookFontRole;
  url: string;
  weight: number;
  style: 'normal' | 'italic';
};

// The template's own logo pair, each a root-relative url or null. showOnDark is
// whether dark slides carry a logo at all. The deck's own logo is LookView.logo.
export type LookLogos = { light: string | null; dark: string | null; showOnDark: boolean };

// The title art: none, a named generator, or an image file at url.
export type LookArt = {
  kind: 'none' | 'generator' | 'image';
  generator: string | null;
  url: string | null;
};

// The ornament's colours as token names; empty means the colours it draws with today.
export type LookOrnament = { colours: PaletteTokenKey[] };

export type TopBar = 'none' | 'accent';

// A named colour the template adds to its palette, with the ink that reads on it.
export type LookExtra = { name: string; colour: string; ink: string };

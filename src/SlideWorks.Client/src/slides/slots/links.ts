// The links layout's slots (K5). Field names are frozen; the server sets the limits.
import type { Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// One link: the title is drawn as the link and `url` is checked by safeHref.
export type LinkItem = {
  kind?: string;
  title: string;
  source?: string;
  url: string;
};

// The code opens `url`; `caption` defaults to the address without its scheme.
export type LinksQr = { url: string; caption?: string };

export type LinksSlots = {
  kicker?: string;
  headline: string;
  links: LinkItem[];
  qr?: LinksQr;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'links',
  'qr',
  'emphasis',
] as const satisfies readonly (keyof LinksSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<LinksSlots, typeof KEYS>>;

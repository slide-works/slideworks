// The rules layout's slots (K5). Field names are frozen; the server sets the limits.
import type { TitleText } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

export type RulesSlots = { kicker?: string; headline: string; rules: TitleText[]; reveal?: Reveal };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'rules',
  'reveal',
] as const satisfies readonly (keyof RulesSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<RulesSlots, typeof KEYS>>;

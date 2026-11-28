import type { LookView } from '../api/types';
import type { AudienceState } from './audienceEvents';

// GET /api/join/{code} (SW-5 J3), the shape of src/contracts/screens/guest.json. No slides are sent up front (D15): the
// slide on screen arrives inside state. It never carries notes, viewers, the owner's email, the timer or deep-dive marks.
export type GuestView = {
  deckTitle: string;
  presenterName: string;
  look: LookView;
  // The player's URL when the deck is shared by link, otherwise null.
  deckUrl: string | null;
  state: AudienceState;
  // The languages the guest can read the slides in (SW-883 K5), null while the deck's translations are off.
  translations: GuestTranslations | null;
};

// One language a guest can pick (SW-883 K5): its name in its own script, its English name, the short label for the
// chip, and how much of the deck is translated into it, as a whole percentage (the primary is 100).
export type GuestLanguage = {
  code: string;
  name: string;
  englishName: string;
  short: string;
  complete: number;
};

// The deck's languages: the primary first, then the deck's list in order.
export type GuestTranslations = {
  primary: string;
  languages: GuestLanguage[];
};

// What the phone does with the call: the 404 body {"error":"no_session"} is the same whatever the reason.
export const NO_SESSION = 'no_session';
export const TOO_MANY_ATTEMPTS = 'too_many_attempts';

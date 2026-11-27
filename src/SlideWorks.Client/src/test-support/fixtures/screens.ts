// The screen JSON examples (K4) from the repo's src/contracts/screens, typed as
// the client's views. The server's shape tests check its records and real
// responses against the same files.
//
// What tsc checks here: every field's name, nesting and value kind, except the
// slides' slots, which a JSON import can't tie to their layout (see JsonView).
// fixtures.test.ts checks the rest at run time: exact key sets, literal values,
// and each slide's slots deep-equal to the sample deck's slide of that layout.
import adminUsersJson from '@contracts/screens/admin-users.json' with { type: 'json' };
import cardJson from '@contracts/screens/card.json' with { type: 'json' };
import consentJson from '@contracts/screens/consent.json' with { type: 'json' };
import deckPageJson from '@contracts/screens/deck-page.json' with { type: 'json' };
import deckPrintJson from '@contracts/screens/deck-print.json' with { type: 'json' };
import deckPlayJson from '@contracts/screens/deck-play.json' with { type: 'json' };
import decksLibraryJson from '@contracts/screens/decks-library.json' with { type: 'json' };
import imageUploadJson from '@contracts/screens/image-upload.json' with { type: 'json' };
import lookChangeJson from '@contracts/screens/look-change.json' with { type: 'json' };
import looksJson from '@contracts/screens/looks.json' with { type: 'json' };
import meJson from '@contracts/screens/me.json' with { type: 'json' };
import profileJson from '@contracts/screens/profile.json' with { type: 'json' };
import sharingJson from '@contracts/screens/sharing.json' with { type: 'json' };
import sampleDeckJson from '@contracts/sample-deck.json' with { type: 'json' };
import type {
  AdminUsersView,
  CardView,
  ConsentView,
  DeckPageView,
  DeckPlayView,
  DeckPrintView,
  DecksLibraryView,
  ImageUploadRequest,
  ImageUploadResult,
  LookChangeRequest,
  LookChangeResult,
  LooksView,
  MeView,
  SharingView,
} from '../../api/types';
import type { ProfileView } from '../../api/profile';
import { DEFAULT_QA_TITLE } from '../../slides/layouts/qaTitle';
import type { LayoutName, Slots } from '../../slides/slots';
import { LAYOUT_SLOTS } from './layoutSlots';
import type { JsonView, Widen } from './widen';

export const me = meJson satisfies Widen<MeView> as MeView;
export const adminUsers = adminUsersJson satisfies Widen<AdminUsersView> as AdminUsersView;
export const decksLibrary = decksLibraryJson satisfies Widen<DecksLibraryView> as DecksLibraryView;
export const deckPage = deckPageJson satisfies JsonView<DeckPageView> as DeckPageView;
export const deckPlay = deckPlayJson satisfies JsonView<DeckPlayView> as DeckPlayView;
type JsonDeckPrint = {
  [K in keyof DeckPrintView]: K extends 'deck' ? JsonView<DeckPlayView> : Widen<DeckPrintView[K]>;
};
export const deckPrint = deckPrintJson satisfies JsonDeckPrint as DeckPrintView;
export const profile = profileJson satisfies Widen<ProfileView> as ProfileView;
export const sharing = sharingJson satisfies Widen<SharingView> as SharingView;
export const consent = consentJson satisfies Widen<ConsentView> as ConsentView;

// SW-2's look screens (L6). look-change.json and image-upload.json each hold a
// request body and its response, typed apart.
export const looks = looksJson satisfies Widen<LooksView> as LooksView;
export const lookChangeRequest =
  lookChangeJson.request satisfies Widen<LookChangeRequest> as LookChangeRequest;
export const lookChangeResult =
  lookChangeJson.response satisfies Widen<LookChangeResult> as LookChangeResult;
export const card = cardJson satisfies Widen<CardView> as CardView;
export const imageUploadRequest =
  imageUploadJson.request satisfies Widen<ImageUploadRequest> as ImageUploadRequest;
export const imageUploadResult =
  imageUploadJson.response satisfies Widen<ImageUploadResult> as ImageUploadResult;

// The sample deck "Ship smaller" (K3): 16 slides covering every layout, in the
// play view's shape. Its slides pass the server's SlideValidator, so it is the
// source of truth for slot contents.
export const sampleDeck = sampleDeckJson satisfies JsonView<DeckPlayView> as DeckPlayView;

// The sample deck's slots for each layout it has (its first slide of that
// layout), as a typed per-layout map: SAMPLE_SLOTS.practice is PracticeSlots.
// Partial, because the sample has no `image` slide (two dividers instead).
// fixtures.test.ts checks it against the sample.
export const SAMPLE_SLOTS = Object.fromEntries(
  [...sampleDeck.slides].reverse().map((slide) => [slide.layout, slide.slots]),
) as { [L in LayoutName]?: Slots[L] };

// SW-6: the play view and the deck page with slide 2 made a poll slide (the mockup's question and four options, as
// LAYOUT_SLOTS.poll). The poll-control.json samples are for slide 4, so pass { slide: POLL_SLIDE_NUMBER } to
// pollControlView() for these decks. The poll's settings are never on the slide (V1), and the server gives a poll slide
// no headline. Each call returns a fresh copy.
export const POLL_SLIDE_NUMBER = 2;

export function deckPlayWithPoll(): DeckPlayView {
  const view = structuredClone(deckPlay);
  view.slides = view.slides.map((slide) =>
    slide.number === POLL_SLIDE_NUMBER
      ? { ...slide, layout: 'poll', steps: 0, slots: structuredClone(LAYOUT_SLOTS.poll) }
      : slide,
  );
  return view;
}

export function deckPageWithPoll(): DeckPageView {
  const view = structuredClone(deckPage);
  view.slides = view.slides.map((slide) =>
    slide.number === POLL_SLIDE_NUMBER
      ? {
          ...slide,
          layout: 'poll',
          headline: null,
          steps: 0,
          slots: structuredClone(LAYOUT_SLOTS.poll),
        }
      : slide,
  );
  return view;
}

// SW-9: the play view and the deck page with slide 3, the last, made a qa slide (LAYOUT_SLOTS.qa's title). The
// qa-control.json samples hold no slide of their own for it, so these decks fit any of them. The deck page's headline is
// the slide's title. Each call returns a fresh copy.
export const QA_SLIDE_NUMBER = 3;

export function deckPlayWithQa(): DeckPlayView {
  const view = structuredClone(deckPlay);
  view.slides = view.slides.map((slide) =>
    slide.number === QA_SLIDE_NUMBER
      ? { ...slide, layout: 'qa', steps: 0, slots: structuredClone(LAYOUT_SLOTS.qa) }
      : slide,
  );
  return view;
}

export function deckPageWithQa(): DeckPageView {
  const view = structuredClone(deckPage);
  view.slides = view.slides.map((slide) =>
    slide.number === QA_SLIDE_NUMBER
      ? {
          ...slide,
          layout: 'qa',
          headline: LAYOUT_SLOTS.qa.title ?? DEFAULT_QA_TITLE,
          steps: 0,
          slots: structuredClone(LAYOUT_SLOTS.qa),
        }
      : slide,
  );
  return view;
}

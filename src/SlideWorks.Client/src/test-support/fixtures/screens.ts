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
import deckPlayJson from '@contracts/screens/deck-play.json' with { type: 'json' };
import decksLibraryJson from '@contracts/screens/decks-library.json' with { type: 'json' };
import imageUploadJson from '@contracts/screens/image-upload.json' with { type: 'json' };
import lookChangeJson from '@contracts/screens/look-change.json' with { type: 'json' };
import looksJson from '@contracts/screens/looks.json' with { type: 'json' };
import meJson from '@contracts/screens/me.json' with { type: 'json' };
import sharingJson from '@contracts/screens/sharing.json' with { type: 'json' };
import sampleDeckJson from '@contracts/sample-deck.json' with { type: 'json' };
import type {
  AdminUsersView,
  CardView,
  ConsentView,
  DeckPageView,
  DeckPlayView,
  DecksLibraryView,
  ImageUploadRequest,
  ImageUploadResult,
  LookChangeRequest,
  LookChangeResult,
  LooksView,
  MeView,
  SharingView,
} from '../../api/types';
import type { LayoutName, Slots } from '../../slides/slots';
import type { JsonView, Widen } from './widen';

export const me = meJson satisfies Widen<MeView> as MeView;
export const adminUsers = adminUsersJson satisfies Widen<AdminUsersView> as AdminUsersView;
export const decksLibrary = decksLibraryJson satisfies Widen<DecksLibraryView> as DecksLibraryView;
export const deckPage = deckPageJson satisfies JsonView<DeckPageView> as DeckPageView;
export const deckPlay = deckPlayJson satisfies JsonView<DeckPlayView> as DeckPlayView;
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

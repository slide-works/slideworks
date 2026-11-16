// The screen JSON examples (K4) from the repo's src/contracts/screens, typed as
// the client's views. The server's shape tests check its records and real
// responses against the same files.
import deckPageJson from '@contracts/screens/deck-page.json' with { type: 'json' };
import deckPlayJson from '@contracts/screens/deck-play.json' with { type: 'json' };
import decksLibraryJson from '@contracts/screens/decks-library.json' with { type: 'json' };
import meJson from '@contracts/screens/me.json' with { type: 'json' };
import sharingJson from '@contracts/screens/sharing.json' with { type: 'json' };
import type {
  DeckPageView,
  DeckPlayView,
  DecksLibraryView,
  MeView,
  SharingView,
} from '../../api/types';
import type { Widen } from './widen';

export const me = meJson satisfies Widen<MeView> as MeView;
export const decksLibrary = decksLibraryJson satisfies Widen<DecksLibraryView> as DecksLibraryView;
export const deckPage = deckPageJson satisfies Widen<DeckPageView> as DeckPageView;
export const deckPlay = deckPlayJson satisfies Widen<DeckPlayView> as DeckPlayView;
export const sharing = sharingJson satisfies Widen<SharingView> as SharingView;

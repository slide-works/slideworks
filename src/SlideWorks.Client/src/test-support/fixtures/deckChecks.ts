import deckChecksJson from '@contracts/screens/deck-checks.json' with { type: 'json' };
import type { DeckChecksView } from '../../api/types';
import type { Widen } from './widen';

// SW-3's checks screen (K2a): the shared sample both sides build against.
export const deckChecksSample = deckChecksJson satisfies Widen<DeckChecksView> as DeckChecksView;

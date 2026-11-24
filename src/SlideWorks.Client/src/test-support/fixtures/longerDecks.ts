import type { DeckPageView, DeckPlayView } from '../../api/types';
import { deckPage, deckPlay } from './screens';

// The fixture deck after an agent added a fourth slide: a copy of slide 2, numbered 4.

/** The play view with a fourth slide. */
export const longerDeckPlay: DeckPlayView = {
  ...deckPlay,
  slides: [...deckPlay.slides, { ...deckPlay.slides[1], number: 4 }],
};

/** The deck page with a fourth slide, which also carries its link. */
export const longerDeckPage: DeckPageView = {
  ...deckPage,
  slides: [...deckPage.slides, { ...deckPage.slides[1], number: 4, link: `${deckPage.playUrl}#4` }],
};

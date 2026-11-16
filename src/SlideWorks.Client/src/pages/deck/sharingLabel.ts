import type { DeckPageView } from '../../api/types';

// Who can open the deck, in the deck page's words.
export function sharingLabel({ level, viewerCount }: DeckPageView['sharing']): string {
  switch (level) {
    case 'private':
      return 'Private';
    case 'link':
      return 'Anyone with the link';
    case 'named':
      return `${viewerCount} named ${viewerCount === 1 ? 'viewer' : 'viewers'}`;
  }
}

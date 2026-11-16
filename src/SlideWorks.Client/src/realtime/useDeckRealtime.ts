import { useEffect, useRef } from 'react';
import type { DeckEvent } from './deckEvents';
import { subscribeToDeck, type SubscribeOptions } from './deckSubscription';

// Follows one deck's events for as long as the calling screen is mounted, and starts over when the deck changes.
// onEvent may change on every render without reconnecting. The options are for tests.
export function useDeckRealtime(
  deckId: string,
  onEvent: (event: DeckEvent) => void,
  options?: SubscribeOptions,
): void {
  const handler = useRef(onEvent);
  useEffect(() => {
    handler.current = onEvent;
  }, [onEvent]);
  useEffect(() => {
    if (deckId === '') return;
    return subscribeToDeck(deckId, (event) => handler.current(event), options);
  }, [deckId, options]);
}

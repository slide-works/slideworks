import { createContext, useContext } from 'react';
import type { LookView } from '../../api/types';

// The deck's look, for the frame's look layers (T8). SlideFrame provides it;
// null outside a slide frame.
export const SlideLookContext = createContext<LookView | null>(null);

// The look of the deck whose slide this is drawn in.
export function useSlideLook(): LookView {
  const look = useContext(SlideLookContext);
  if (look === null) throw new Error('useSlideLook must be used inside a SlideFrame');
  return look;
}

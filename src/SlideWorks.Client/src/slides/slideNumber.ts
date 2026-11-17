import { createContext, useContext } from 'react';

// The slide's number (1-based), or null outside a slide. LayoutProps (K12) carries slots, step and deck but not the
// slide's number, and the poll slide needs it to tell its own live data from another poll's (SW-6 V7), so
// SlideRenderer provides it here, as it does the part.
export const SlideNumberContext = createContext<number | null>(null);

export function useSlideNumber(): number | null {
  return useContext(SlideNumberContext);
}

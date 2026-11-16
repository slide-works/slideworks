import { createContext, useContext } from 'react';

// The slide's part (1-based, or null when it belongs to none). LayoutProps (K12)
// carries slots, step and deck but not the slide's part, and the divider needs
// it for its number and journey, so SlideRenderer provides it here.
export const SlidePartContext = createContext<number | null>(null);

export function useSlidePart(): number | null {
  return useContext(SlidePartContext);
}

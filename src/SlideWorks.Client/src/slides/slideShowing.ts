import { createContext, useContext } from 'react';

// True when this slide is the one on screen. The player keeps every slide in
// the DOM and hides the rest, so a video must drop its player when its slide
// is hidden or the sound keeps going. With no provider, the slide is showing:
// a thumbnail sets interactive to false instead of hiding the slide.
export const SlideShowingContext = createContext(true);

export function useSlideShowing(): boolean {
  return useContext(SlideShowingContext);
}

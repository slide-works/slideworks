import type { DeckPageSlide } from '../../api/types';

// "Slides 10, 11, 12" for the slides flagged as deep dives, or null when none are.
export function deepDiveNumbers(slides: DeckPageSlide[]): string | null {
  const numbers = slides.filter((slide) => slide.deepDive).map((slide) => slide.number);
  if (numbers.length === 0) return null;
  return `${numbers.length === 1 ? 'Slide' : 'Slides'} ${numbers.join(', ')}`;
}

import { Route, Routes } from 'react-router-dom';
import type { SlideFinding } from '../api/types';
import { DeckPage } from '../pages/DeckPage';

declare global {
  interface Window {
    // Component tests only: the slide numbers the stand-in measure was called with, in order.
    swMeasureCalls?: number[];
  }
}

// Component tests only: the deck page with a stand-in for measureSlide. A CT prop that is a function runs in Node, so
// the stand-in is defined here, in the browser bundle, and answers with the findings the test passes as data.
export function DeckPageChecksHarness({ measured }: { measured: SlideFinding[] }) {
  const measure = (_frame: HTMLElement, slide: number) => {
    (window.swMeasureCalls ??= []).push(slide);
    return measured.filter((finding) => finding.slide === slide);
  };
  return (
    <Routes>
      <Route path="/decks/:id" element={<DeckPage measure={measure} />} />
    </Routes>
  );
}

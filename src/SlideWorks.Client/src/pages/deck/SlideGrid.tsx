import type { DeckPageView, SlideFinding } from '../../api/types';
import { deepDivesOf } from '../../look/deepDivesOf';
import type { SlideDeck } from '../../slides/slots';
import { SlideThumb } from './SlideThumb';

type SlideGridProps = {
  view: DeckPageView;
  findings: ReadonlyMap<number, SlideFinding[]>;
  selected: number;
  onSelect(slideNumber: number): void;
};

// Every slide of the deck as a thumbnail, in order.
export function SlideGrid({ view, findings, selected, onSelect }: SlideGridProps) {
  // The same deck the player shows, so thumbnail footers match it.
  const deck: SlideDeck = {
    title: view.title,
    author: view.author,
    parts: view.parts,
    look: view.look,
    deepDives: deepDivesOf(view.slides),
    interactive: false,
  };
  if (view.slides.length === 0) {
    return (
      <p
        data-testid="deck-no-slides"
        className="panel px-6 py-10 text-center text-meta text-ink-muted"
      >
        No slides yet. Ask a connected agent to add some.
      </p>
    );
  }
  return (
    <ol data-testid="deck-thumbs" className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-4">
      {view.slides.map((slide) => (
        <SlideThumb
          key={slide.number}
          slide={slide}
          deck={deck}
          total={view.slides.length}
          findings={findings.get(slide.number) ?? []}
          selected={slide.number === selected}
          onSelect={() => onSelect(slide.number)}
        />
      ))}
    </ol>
  );
}

import { Link } from 'react-router-dom';
import type { DeckPageSlide } from '../../api/types';

// The selected slide's details: layout, steps, the link that opens the player
// at it, and its speaker notes (only ever shown here, never to viewers).
export function SelectedSlide({ slide }: { slide: DeckPageSlide }) {
  return (
    <section data-testid="selected-slide" className="panel grid gap-3 p-4">
      <h3 className="text-xs font-semibold tracking-wide text-ink-muted uppercase">
        Slide <span data-testid="selected-number">{slide.number}</span>
      </h3>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-meta">
        <dt className="text-ink-muted">Layout</dt>
        <dd data-testid="selected-layout" className="font-mono text-ink">
          {slide.layout}
        </dd>
        <dt className="text-ink-muted">Steps</dt>
        <dd data-testid="selected-steps" className="text-ink">
          {slide.steps}
        </dd>
        <dt className="text-ink-muted">Link</dt>
        <dd className="min-w-0 truncate">
          <Link data-testid="selected-link" to={slide.link} className="font-mono text-accent">
            {slide.link}
          </Link>
        </dd>
      </dl>
      {slide.notes === null ? (
        <p data-testid="selected-notes" className="text-meta text-ink-faint">
          No speaker notes.
        </p>
      ) : (
        <p
          data-testid="selected-notes"
          className="rounded-md border border-warn/25 bg-warn-soft px-3 py-2 text-meta whitespace-pre-line text-ink"
        >
          {slide.notes}
        </p>
      )}
    </section>
  );
}

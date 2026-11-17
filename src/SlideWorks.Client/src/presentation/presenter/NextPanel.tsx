import type { PresenterView } from '../../api/types';
import type { NextPreview } from '../navigate';
import { PresenterThumb } from '../PresenterThumb';

type Props = {
  view: PresenterView;
  preview: NextPreview;
  // Deep dives after the current slide, and whether next skips them.
  deepDivesAhead: number;
  skipDeepDives: boolean;
};

// What next shows: the slide's next reveal, the next slide (naming the deep dives it skips), or the end of the deck.
export function NextPanel({ view, preview, deepDivesAhead, skipDeepDives }: Props) {
  return (
    <section
      data-testid="presenter-next"
      aria-label="Next"
      className="grid min-w-0 content-start gap-2"
    >
      <h2 className="flex items-center text-[11px] font-semibold tracking-[0.12em] text-[#94a3b8] uppercase">
        Next
        <span
          data-testid="presenter-next-label"
          className="ml-auto font-mono text-xs tracking-normal text-[#e2e8f0]"
        >
          {labelOf(view, preview)}
        </span>
      </h2>
      <div className="overflow-hidden rounded-lg bg-[#111827]">
        {preview.kind === 'end' ? (
          <p className="grid aspect-video place-content-center text-sm text-[#94a3b8]">
            End of the deck
          </p>
        ) : (
          <PresenterThumb
            view={view}
            slide={preview.slide}
            step={preview.kind === 'step' ? preview.step : 0}
          />
        )}
      </div>
      <p data-testid="presenter-next-note" className="min-h-4 text-xs text-[#94a3b8]">
        {noteOf(view, preview, deepDivesAhead, skipDeepDives)}
      </p>
    </section>
  );
}

function labelOf(view: PresenterView, preview: NextPreview): string {
  switch (preview.kind) {
    case 'step':
      return `reveal ${preview.step} of ${view.slides[preview.slide - 1]?.steps ?? preview.step}`;
    case 'slide':
      return view.slides[preview.slide - 1]?.deepDive
        ? `slide ${preview.slide} · deep dive`
        : `slide ${preview.slide}`;
    case 'end':
      return 'end of deck';
  }
}

// The deep dives next steps over, by number and headline; otherwise how many deep dives are still ahead.
function noteOf(
  view: PresenterView,
  preview: NextPreview,
  ahead: number,
  skipping: boolean,
): string {
  if (preview.kind === 'slide' && preview.skipped.length > 0) {
    const names = preview.skipped.map((n) => {
      const headline = view.slides[n - 1]?.headline;
      return headline ? `${n}: ${headline}` : String(n);
    });
    return `Skipping deep dive${names.length > 1 ? 's' : ''} ${names.join('; ')}`;
  }
  if (ahead === 0) return '';
  return `${ahead} deep dive${ahead > 1 ? 's' : ''} ahead${skipping ? ', skipping' : ''}`;
}

import type { PresenterView } from '../../api/types';
import { PresenterThumb } from '../../presentation/PresenterThumb';
import { deepDivesAhead, type NextPreview } from '../../presentation/navigate';

type Props = {
  view: PresenterView;
  slide: number;
  preview: NextPreview;
  deepDives: ReadonlySet<number>;
  skipDeepDives: boolean;
};

// What Next shows, in words: "reveal k of n", "slide n", or the end of the deck.
export function nextLabel(view: PresenterView, preview: NextPreview): string {
  switch (preview.kind) {
    case 'step':
      return `reveal ${preview.step} of ${view.slides[preview.slide - 1]?.steps ?? preview.step}`;
    case 'slide':
      return `slide ${preview.slide}`;
    case 'end':
      return 'end of the deck';
  }
}

function deepDiveNote(
  slide: number,
  preview: NextPreview,
  deepDives: ReadonlySet<number>,
  skipDeepDives: boolean,
): string | null {
  if (preview.kind === 'slide' && preview.skipped.length > 0) {
    const [first, ...rest] = preview.skipped;
    return rest.length === 0
      ? `skipping deep dive ${first}`
      : `skipping deep dives ${preview.skipped.join(', ')}`;
  }
  if (skipDeepDives) return null;
  const ahead = deepDivesAhead({ slide, step: 0 }, deepDives);
  if (ahead === 0) return null;
  return ahead === 1 ? '1 deep dive ahead' : `${ahead} deep dives ahead`;
}

// The next row: a thumbnail of what Next shows, the words for it, and the deep dives it steps over (or that lie
// ahead while they aren't skipped).
export function NextRow({ view, slide, preview, deepDives, skipDeepDives }: Props) {
  const note = deepDiveNote(slide, preview, deepDives, skipDeepDives);
  return (
    <div
      data-testid="speaker-next-row"
      className="mx-4 grid grid-cols-[92px_minmax(0,1fr)] items-center gap-3"
    >
      <div className="overflow-hidden rounded-sm opacity-90">
        {preview.kind === 'end' ? (
          <div className="aspect-video w-full bg-slate-800" />
        ) : (
          <PresenterThumb
            view={view}
            slide={preview.slide}
            step={preview.kind === 'step' ? preview.step : 0}
          />
        )}
      </div>
      <div>
        <div className="text-[10px] font-semibold tracking-widest text-slate-400 uppercase">
          Next
        </div>
        <b data-testid="speaker-next-label" className="text-sm font-medium">
          {nextLabel(view, preview)}
        </b>
        {note && (
          <div data-testid="speaker-next-note" className="mt-0.5 text-xs text-violet-300">
            {note}
          </div>
        )}
      </div>
    </div>
  );
}

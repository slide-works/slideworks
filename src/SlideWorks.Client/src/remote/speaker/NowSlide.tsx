import type { PresenterView } from '../../api/types';
import { PresenterThumb } from '../../presentation/PresenterThumb';

type Props = { view: PresenterView; slide: number; step: number; black: boolean };

// The current slide at the phone's full width, at its current step, so the speaker sees what the room sees (D6). With
// blackout on, a notice under it says the room sees a black screen.
export function NowSlide({ view, slide, step, black }: Props) {
  const headline = view.slides[slide - 1]?.headline;
  return (
    <div className="grid gap-2 px-4">
      <figure
        data-testid="speaker-now"
        data-slide={slide}
        data-step={step}
        aria-label={
          headline ? `Now showing slide ${slide}: ${headline}` : `Now showing slide ${slide}`
        }
        className="m-0 overflow-hidden rounded outline-2 outline-offset-1 outline-indigo-300/60"
      >
        <PresenterThumb view={view} slide={slide} step={step} />
      </figure>
      {black && (
        <p
          data-testid="speaker-black-notice"
          role="status"
          className="m-0 rounded-lg bg-white px-3 py-1.5 text-center text-xs font-semibold text-black"
        >
          The room sees a black screen
        </p>
      )}
    </div>
  );
}

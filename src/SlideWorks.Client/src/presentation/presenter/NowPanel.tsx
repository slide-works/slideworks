import type { PresenterView } from '../../api/types';
import { PresenterThumb } from '../PresenterThumb';

type Props = {
  view: PresenterView;
  slide: number;
  step: number;
  black: boolean;
  // The accessible names of the move buttons, naming where each goes ("Next: slide 9").
  nextName: string;
  previousName: string;
  onNext(): void;
  onPrevious(): void;
};

// The presenter window's largest pane: the slide the audience sees now, at its current step, with the count, the
// reveal dots, the progress bar and the previous and next buttons. While the audience sees black it says so on top.
export function NowPanel({
  view,
  slide,
  step,
  black,
  nextName,
  previousName,
  onNext,
  onPrevious,
}: Props) {
  const total = view.slides.length;
  const steps = view.slides[slide - 1]?.steps ?? 0;
  const count = `${slide} / ${total}`;
  return (
    <section
      data-testid="presenter-now"
      aria-label="Now"
      className="grid min-w-0 content-start gap-3"
    >
      <h2 className="flex items-center text-[11px] font-semibold tracking-[0.12em] text-[#94a3b8] uppercase">
        Now
        <span
          data-testid="presenter-count"
          className="ml-auto font-mono text-xs tracking-normal text-[#e2e8f0]"
        >
          {count}
        </span>
      </h2>

      <div className="relative overflow-hidden rounded-lg ring-2 ring-[#8ea5ff]/60">
        <PresenterThumb view={view} slide={slide} step={step} />
        {black && (
          <p
            data-testid="presenter-black-badge"
            className="absolute inset-0 grid place-content-center gap-1 bg-black/85 text-center text-sm font-semibold text-white"
          >
            Audience sees a black screen
            <span className="font-normal opacity-80">B to bring the slide back</span>
          </p>
        )}
      </div>

      <div className="flex items-center gap-3">
        <button
          type="button"
          data-testid="presenter-previous"
          aria-label={previousName}
          title={previousName}
          className="grid size-8 place-items-center rounded-md bg-white/10 hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
          onClick={onPrevious}
        >
          <svg
            viewBox="0 0 24 24"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.2"
            aria-hidden
          >
            <path d="M15 6l-6 6 6 6" />
          </svg>
        </button>
        <button
          type="button"
          data-testid="presenter-next-button"
          aria-label={nextName}
          title={nextName}
          className="grid size-8 place-items-center rounded-md bg-white/10 hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
          onClick={onNext}
        >
          <svg
            viewBox="0 0 24 24"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.2"
            aria-hidden
          >
            <path d="M9 6l6 6-6 6" />
          </svg>
        </button>
        <span data-testid="presenter-position" className="min-w-16 text-center font-mono text-sm">
          {step > 0 ? `${slide}.${step} / ${total}` : count}
        </span>
        {steps > 0 && (
          <span className="flex gap-1" aria-label={`Reveal ${step} of ${steps}`} role="img">
            {Array.from({ length: steps }, (_, at) => (
              <i
                key={at}
                className={`size-[7px] rounded-full ${at < step ? 'bg-[#a5b8ff]' : 'bg-white/20'}`}
              />
            ))}
          </span>
        )}
        <span className="relative h-1 flex-1 overflow-hidden rounded-sm bg-white/10" aria-hidden>
          <i
            className="absolute inset-y-0 left-0 bg-[#6d8bff] motion-safe:transition-[width] motion-safe:duration-300"
            style={{ width: `${(slide / total) * 100}%` }}
          />
        </span>
      </div>
    </section>
  );
}

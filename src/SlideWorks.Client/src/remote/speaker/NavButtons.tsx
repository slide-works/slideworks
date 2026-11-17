import type { Ref } from 'react';

type Props = {
  // What each button goes to, in words with the slide's headline ("slide 7: Slice it thin"), or null at the start or the
  // end of the deck.
  previous: string | null;
  next: string | null;
  disabled: boolean;
  onPrevious(): void;
  onNext(): void;
  // The Next button, so the page can put focus there after a jump.
  nextRef?: Ref<HTMLButtonElement>;
  // The Previous button, for focus when Next is disabled under it at the end of the deck.
  previousRef?: Ref<HTMLButtonElement>;
};

// Previous (small) and Next (large, under the thumb). Disabled while not live: a tap is never queued for later (P8).
export function NavButtons({
  previous,
  next,
  disabled,
  onPrevious,
  onNext,
  nextRef,
  previousRef,
}: Props) {
  return (
    <div className="grid grid-cols-[76px_minmax(0,1fr)] gap-2 px-4 pt-2.5 pb-1.5">
      <button
        ref={previousRef}
        type="button"
        data-testid="speaker-previous"
        aria-label={previous ? `Previous: ${previous}` : 'Previous: at the start of the deck'}
        disabled={disabled || previous === null}
        onClick={onPrevious}
        className="grid h-14 place-items-center rounded-2xl bg-white/10 text-slate-200 focus-visible:outline-2 focus-visible:outline-indigo-300 active:scale-[.98] disabled:opacity-40 motion-reduce:active:scale-100"
      >
        <svg
          aria-hidden
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.4"
          className="size-5"
        >
          <path d="M15 6l-6 6 6 6" />
        </svg>
      </button>
      <button
        ref={nextRef}
        type="button"
        data-testid="speaker-next"
        aria-label={next ? `Next: ${next}` : 'Next: at the end of the deck'}
        disabled={disabled || next === null}
        onClick={onNext}
        className="grid h-14 place-items-center rounded-2xl bg-[#3b5bdb] text-base font-semibold text-white focus-visible:outline-2 focus-visible:outline-indigo-300 active:scale-[.98] disabled:opacity-40 motion-reduce:active:scale-100"
      >
        Next
      </button>
    </div>
  );
}

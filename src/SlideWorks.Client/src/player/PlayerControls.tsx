import { useEffect, useState } from 'react';
import { useCopyText } from '../components/useCopyText';
import { LiveChip } from '../live/LiveChip';
import { formatHash, type PlayerPosition } from './parseHash';

type PlayerControlsProps = {
  deckTitle: string;
  position: PlayerPosition;
  total: number;
  steps: number;
  canFullscreen: boolean;
  fullscreen: boolean;
  onPrevious(): void;
  onNext(): void;
  onFullscreen(): void;
  // The deck's look.joinBar, so the live chip's toggle follows a change made anywhere.
  joinBar: boolean;
  // False in the audience window, which follows the talk and never goes live itself. Defaults to true.
  liveChip?: boolean;
};

const BUTTON =
  'grid size-9 shrink-0 cursor-pointer place-items-center rounded-lg bg-white/10 text-slate-200 ' +
  'hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#93a8ff]';

const SHOW_MESSAGE_MS = 2500;

// Copies this page's address with the player's hash for the current slide. A
// short message above the button says it worked, or that the browser refused
// (the person then copies the address bar by hand). The message goes away on
// its own, and the parent's key on the position clears it when the slide moves.
function CopyLinkButton({ position }: { position: PlayerPosition }) {
  const copy = useCopyText();
  const [clicks, setClicks] = useState(0);
  const [dismissed, setDismissed] = useState(false);
  useEffect(() => {
    if (clicks === 0) return;
    const timer = window.setTimeout(() => setDismissed(true), SHOW_MESSAGE_MS);
    return () => window.clearTimeout(timer);
  }, [clicks]);
  const shown = copy.state !== 'idle' && !dismissed;
  const onClick = () => {
    const url = new URL(window.location.href);
    url.hash = formatHash(position);
    setDismissed(false);
    setClicks((count) => count + 1);
    copy.copy(url.href);
  };
  const message =
    copy.state === 'copied'
      ? 'Link copied'
      : copy.state === 'failed'
        ? "Couldn't copy · copy the address bar instead"
        : '';
  return (
    <span className="relative shrink-0">
      <button
        type="button"
        data-testid="player-copy-link"
        aria-label="Copy link to this slide"
        title="Copy link to this slide"
        className={BUTTON}
        onClick={onClick}
      >
        <svg
          viewBox="0 0 24 24"
          className="size-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          aria-hidden="true"
        >
          <path d="M10 14a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1M14 10a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1" />
        </svg>
      </button>
      <span
        data-testid="player-copy-link-message"
        role={copy.state === 'failed' ? 'alert' : 'status'}
        hidden={!shown}
        className="absolute right-0 bottom-full z-10 mb-2 w-max max-w-[min(18rem,calc(100vw-2rem))] rounded-lg bg-slate-900 px-3 py-1.5 text-center text-xs text-slate-100 shadow-lg"
      >
        {shown ? message : ''}
      </span>
    </span>
  );
}

// The bar under the stage: previous and next, "n / total", one dot per step of
// the current slide, the deck's progress, copy link, and fullscreen (or exit fullscreen).
export function PlayerControls({
  deckTitle,
  position,
  total,
  steps,
  canFullscreen,
  fullscreen,
  onPrevious,
  onNext,
  onFullscreen,
  joinBar,
  liveChip = true,
}: PlayerControlsProps) {
  const progress = total > 0 ? (position.slide / total) * 100 : 0;
  return (
    <div
      data-testid="player-controls"
      className="flex h-14 shrink-0 items-center gap-2.5 px-4 font-sans text-[12.5px] font-medium text-slate-300"
    >
      <button
        type="button"
        data-testid="player-previous"
        aria-label="Previous"
        className={BUTTON}
        onClick={onPrevious}
      >
        <svg
          viewBox="0 0 24 24"
          className="size-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.2"
          aria-hidden="true"
        >
          <path d="M15 6l-6 6 6 6" />
        </svg>
      </button>
      <button
        type="button"
        data-testid="player-next"
        aria-label="Next"
        className={BUTTON}
        onClick={onNext}
      >
        <svg
          viewBox="0 0 24 24"
          className="size-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.2"
          aria-hidden="true"
        >
          <path d="M9 6l6 6-6 6" />
        </svg>
      </button>
      <span
        data-testid="player-count"
        className="min-w-16 text-center font-mono"
        aria-live="polite"
      >
        {position.slide} / {total}
      </span>
      <span
        data-testid="player-steps"
        className="flex gap-1"
        role="img"
        aria-label={`Step ${position.step} of ${steps}`}
        hidden={steps === 0}
      >
        {Array.from({ length: steps }, (_, index) => (
          <i
            key={index}
            className={`size-1.5 rounded-full ${index < position.step ? 'bg-[#a5b8ff]' : 'bg-white/20'}`}
          />
        ))}
      </span>
      <span className="h-1 min-w-0 flex-1 overflow-hidden rounded-sm bg-white/10">
        <i
          data-testid="player-progress"
          className="block h-full bg-[#6d8bff] motion-safe:transition-[width] motion-safe:duration-300"
          style={{ width: `${progress}%` }}
        />
      </span>
      {liveChip && <LiveChip deckTitle={deckTitle} joinBar={joinBar} />}
      <CopyLinkButton key={formatHash(position)} position={position} />
      {canFullscreen && (
        <button
          type="button"
          data-testid="player-fullscreen"
          aria-label={fullscreen ? 'Exit fullscreen' : 'Fullscreen'}
          title={fullscreen ? 'Exit fullscreen (F)' : 'Fullscreen (F)'}
          className={BUTTON}
          onClick={onFullscreen}
        >
          <svg
            viewBox="0 0 24 24"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
          >
            <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />
          </svg>
        </button>
      )}
    </div>
  );
}

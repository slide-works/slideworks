import { useRef, useState, type ReactNode, type RefObject } from 'react';
import { GUEST_TEST_IDS } from '../testIds';
import { enterFullscreen, fullscreenSupported } from './fullscreen';
import { useOverlayTimer } from './useOverlayTimer';

// What a phone turned sideways shows over the full-screen slide (SW-883 D6): the slide number, the Live chip, the
// language chip and the progress, for 3 seconds after a tap, and Full screen where the browser allows it. index and
// count are the slide on screen, connected is false while the connection comes back, progress is index / count (0 to
// 1), and languageChip is the language chip, or null when the deck has no translations. The layer is display: none
// outside landscape, so the header's chip is the only one on screen there; the slide mirror is inert, so the layer owns
// the tap surface.
export function LandscapeLayer(props: {
  index: number;
  count: number;
  connected: boolean;
  progress: number;
  languageChip: ReactNode | null;
}): ReactNode {
  const overlayRef = useRef<HTMLDivElement | null>(null);
  const tapRef = useRef<HTMLButtonElement | null>(null);
  const { visible, toggle, hide, keepAlive } = useOverlayTimer(overlayRef);

  // A tap that hides the overlay while focus is inside it leaves focus on the tap surface, not on the page body.
  const hideFromTap = () => {
    if (overlayRef.current?.contains(document.activeElement)) tapRef.current?.focus();
    hide();
  };

  return (
    <div className="hidden [@media(orientation:landscape)_and_(max-height:500px)]:contents">
      <button
        ref={tapRef}
        type="button"
        aria-label="Show slide controls"
        aria-expanded={visible}
        onClick={visible ? hideFromTap : toggle}
        className="fixed inset-0 z-10 cursor-default bg-transparent"
      />
      {visible && <Overlay {...props} overlayRef={overlayRef} keepAlive={keepAlive} />}
    </div>
  );
}

// Mounted only while the overlay shows, so a refusal message goes away with the overlay, timer hide included.
function Overlay({
  index,
  count,
  connected,
  progress,
  languageChip,
  overlayRef,
  keepAlive,
}: {
  index: number;
  count: number;
  connected: boolean;
  progress: number;
  languageChip: ReactNode | null;
  overlayRef: RefObject<HTMLDivElement | null>;
  keepAlive: () => void;
}): ReactNode {
  const [refused, setRefused] = useState(false);
  const canFullscreen = fullscreenSupported();

  const requestFullscreen = async () => {
    keepAlive();
    setRefused(!(await enterFullscreen()));
  };

  return (
    <div
      ref={overlayRef}
      data-testid={GUEST_TEST_IDS.landscapeOverlay}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) keepAlive();
      }}
      className="pointer-events-none fixed inset-0 z-20 flex flex-col justify-between text-(--sw-night-ink)"
    >
      <div className="flex items-center gap-2 bg-black/60 px-4 py-2 text-xs">
        <span>
          {index} / {count}
        </span>
        <span className="rounded-full bg-white/15 px-2 py-0.5">
          {connected ? 'Live' : 'Offline'}
        </span>
        {languageChip !== null && <span className="pointer-events-auto">{languageChip}</span>}
        {canFullscreen && (
          <button
            type="button"
            data-testid={GUEST_TEST_IDS.landscapeFullscreen}
            onClick={requestFullscreen}
            className="pointer-events-auto ml-auto rounded-full bg-white/15 px-3 py-1"
          >
            Full screen
          </button>
        )}
      </div>
      {refused && (
        <p
          role="alert"
          data-testid={GUEST_TEST_IDS.landscapeFullscreenError}
          className="mx-auto rounded-md bg-black/70 px-3 py-1 text-xs"
        >
          Full screen isn&apos;t available here
        </p>
      )}
      <div className="h-1 w-full bg-white/20">
        <div
          className="h-full bg-white"
          style={{ width: `${Math.round(Math.min(1, Math.max(0, progress)) * 100)}%` }}
        />
      </div>
    </div>
  );
}

import { useMemo, useRef } from 'react';
import { CANVAS_HEIGHT, CANVAS_WIDTH, useCanvasScale } from '../player/useCanvasScale';
import { SlideRenderer } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import { ActivitySlot } from './ActivitySlot';
import type { AudienceCurrent, AudienceState } from './audienceEvents';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The slide on screen, drawn at the 1920×1080 canvas size and scaled to the width it is given, in the deck's look. A
// black screen is a plain black box. It is a picture: inert and hidden from assistive tech, named by the caption around it.
function MirroredSlide({ view, current }: { view: GuestView; current: AudienceCurrent }) {
  const frame = useRef<HTMLDivElement>(null);
  const canvasFit = useCanvasScale(frame);
  // The guest gets no parts or deep dives (D15): a slide shows no part tag, and the footer author is the presenter.
  const deck = useMemo<SlideDeck>(
    () => ({
      title: view.deckTitle,
      author: view.presenterName,
      parts: [],
      look: view.look,
      deepDives: [],
    }),
    [view.deckTitle, view.presenterName, view.look],
  );

  return (
    <div
      ref={frame}
      data-testid={GUEST_TEST_IDS.slide}
      data-slide={current.index}
      data-step={current.step}
      data-black={current.black}
      aria-hidden
      inert
      className="relative aspect-video w-full overflow-hidden bg-black [@media(orientation:landscape)_and_(max-height:500px)]:w-[min(100vw,calc(100dvh*16/9))]"
    >
      {!current.black && (
        <div
          className="absolute top-0 left-0 overflow-hidden"
          style={{
            width: CANVAS_WIDTH,
            height: CANVAS_HEIGHT,
            transformOrigin: '0 0',
            transform: canvasFit ? `scale(${canvasFit.scale})` : undefined,
            visibility: canvasFit ? undefined : 'hidden',
          }}
        >
          <SlideRenderer
            slide={{ ...current, number: current.index, part: null, deepDive: false }}
            deck={deck}
            total={current.count}
            step={current.step}
          />
        </div>
      )}
    </div>
  );
}

// The slide mirror with a progress strip, and the activity card below it. In landscape the slide fills the screen and the
// rest is left out, which helps people far from the projector.
export function GuestFollowing({ view, state }: { view: GuestView; state: AudienceState }) {
  const current = state.current;
  return (
    <main
      data-testid={GUEST_TEST_IDS.following}
      className="flex min-h-dvh w-full max-w-full flex-col gap-3 overflow-x-hidden bg-neutral-950 px-4 pt-4 pb-6 text-white [@media(orientation:landscape)_and_(max-height:500px)]:items-center [@media(orientation:landscape)_and_(max-height:500px)]:justify-center [@media(orientation:landscape)_and_(max-height:500px)]:p-0"
    >
      <header className="[@media(orientation:landscape)_and_(max-height:500px)]:hidden">
        <h1 data-testid={GUEST_TEST_IDS.deckTitle} className="truncate text-lg font-semibold">
          {view.deckTitle}
        </h1>
        <p data-testid={GUEST_TEST_IDS.presenter} className="truncate text-sm text-white/70">
          {view.presenterName}
        </p>
      </header>
      {current && <MirroredSlide view={view} current={current} />}
      <p
        data-testid={GUEST_TEST_IDS.progress}
        aria-label={current ? `Slide ${current.index} of ${current.count}` : undefined}
        className="text-sm text-white/70 tabular-nums [@media(orientation:landscape)_and_(max-height:500px)]:hidden"
      >
        {current ? `${current.index} / ${current.count}` : ''}
      </p>
      <div className="[@media(orientation:landscape)_and_(max-height:500px)]:hidden">
        <ActivitySlot />
      </div>
    </main>
  );
}
